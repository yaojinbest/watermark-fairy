using System.IO;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using WatermarkFairy.Models;
using WatermarkFairy.Services;
using WatermarkFairy.ViewModels;
using Xunit;

namespace WatermarkFairy.Tests;

public class MainViewModelTests
{
    private readonly MainViewModel _vm = new();

    // ============ 构造 + 默认 ============

    [Fact]
    public void Constructor_DefaultConfig_HasDefaultTextLayer()
    {
        var vm = new MainViewModel();
        vm.Config.Should().NotBeNull();
        vm.Config.Layers.Should().HaveCount(1);
        vm.Config.Layers[0].Should().BeOfType<TextWatermarkLayer>();
        vm.FileList.Should().BeEmpty();
        vm.StatusText.Should().NotBeNullOrEmpty();
        vm.ProgressPercent.Should().Be(0);
        vm.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDeps_AcceptsCustom()
    {
        var processor = new ImageProcessor();
        var store = new TemplateStore();
        var vm = new MainViewModel(processor, store);
        vm.Should().NotBeNull();
    }

    [Fact]
    public void HasFiles_Empty_False()
    {
        _vm.HasFiles.Should().BeFalse();
    }

    [Fact]
    public void HasFiles_NonEmpty_True()
    {
        _vm.AddFile(CreateTempImage("a.png"));
        _vm.HasFiles.Should().BeTrue();
    }

    // ============ AddFile ============

    [Fact]
    public void AddFile_ValidPng_ReturnsTrue_AddsToList()
    {
        var path = CreateTempImage("a.png");
        _vm.AddFile(path).Should().BeTrue();
        _vm.FileList.Should().ContainSingle();
        _vm.FileList[0].Should().Be(path);
    }

    [Fact]
    public void AddFile_NonExistent_ReturnsFalse()
    {
        _vm.AddFile("/tmp/nonexistent.png").Should().BeFalse();
        _vm.FileList.Should().BeEmpty();
    }

    [Fact]
    public void AddFile_EmptyOrWhitespace_ReturnsFalse()
    {
        _vm.AddFile("").Should().BeFalse();
        _vm.AddFile("  ").Should().BeFalse();
        _vm.AddFile(null!).Should().BeFalse();
        _vm.FileList.Should().BeEmpty();
    }

    [Fact]
    public void AddFile_Duplicate_ReturnsFalse()
    {
        var path = CreateTempImage("a.png");
        _vm.AddFile(path).Should().BeTrue();
        _vm.AddFile(path).Should().BeFalse();
        _vm.FileList.Should().ContainSingle();
    }

    [Fact]
    public void AddFile_UpdatesStatusText()
    {
        var path = CreateTempImage("a.png");
        _vm.AddFile(path);
        _vm.StatusText.Should().Contain(Path.GetFileName(path));
    }

    // ============ AddFolder ============

    [Fact]
    public void AddFolder_WithMixedFiles_OnlyAddsImages()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wf_folder_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        CreateTempImage(Path.Combine(folder, "a.png"));
        CreateTempImage(Path.Combine(folder, "b.jpg"));
        File.WriteAllText(Path.Combine(folder, "c.txt"), "not image");
        File.WriteAllText(Path.Combine(folder, "d.mp4"), "not image");

        var added = _vm.AddFolder(folder);
        added.Should().Be(2);
        _vm.FileList.Count.Should().Be(2);

        Directory.Delete(folder, true);
    }

    [Fact]
    public void AddFolder_NonExistent_ReturnsZero()
    {
        _vm.AddFolder("/nonexistent/folder").Should().Be(0);
    }

    [Fact]
    public void AddFolder_NoImages_ReturnsZero()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"wf_empty_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.txt"), "x");

        _vm.AddFolder(folder).Should().Be(0);

        Directory.Delete(folder, true);
    }

    [Fact]
    public void AddFolder_Recursive_ScansSubdirs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"wf_root_{Guid.NewGuid():N}");
        var sub = Path.Combine(root, "sub");
        Directory.CreateDirectory(sub);
        CreateTempImage(Path.Combine(root, "root.png"));
        CreateTempImage(Path.Combine(sub, "sub.png"));

        var added = _vm.AddFolder(root);
        added.Should().Be(2);

        Directory.Delete(root, true);
    }

    // ============ RemoveFile / ClearFiles ============

    [Fact]
    public void RemoveFile_Existing_ReturnsTrue()
    {
        var p = CreateTempImage("a.png");
        _vm.AddFile(p);
        _vm.RemoveFile(p).Should().BeTrue();
        _vm.FileList.Should().BeEmpty();
    }

    [Fact]
    public void RemoveFile_NonExisting_ReturnsFalse()
    {
        _vm.RemoveFile("/nonexistent.png").Should().BeFalse();
    }

    [Fact]
    public void ClearFiles_NonEmpty_RemovesAll()
    {
        _vm.AddFile(CreateTempImage("a.png"));
        _vm.AddFile(CreateTempImage("b.png"));
        _vm.FileList.Count.Should().Be(2);
        _vm.ClearFiles();
        _vm.FileList.Should().BeEmpty();
    }

    [Fact]
    public void ClearFiles_Empty_DoesNotChange()
    {
        _vm.ClearFiles();
        _vm.FileList.Should().BeEmpty();
    }

    // ============ ApplyWatermarkAsync ============

    [Fact]
    public async Task ApplyWatermarkAsync_EmptyList_DoesNothing()
    {
        _vm.OutputFolder = Path.Combine(Path.GetTempPath(), $"wf_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_vm.OutputFolder);
        await _vm.ApplyWatermarkAsync(_vm.OutputFolder);
        _vm.StatusText.Should().Contain("添加图片");
        Directory.Delete(_vm.OutputFolder, true);
    }

    [Fact]
    public async Task ApplyWatermarkAsync_WithFiles_ProcessesAll()
    {
        var input = Path.Combine(Path.GetTempPath(), $"wf_apply_{Guid.NewGuid():N}");
        var output = Path.Combine(Path.GetTempPath(), $"wf_apply_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(output);

        var f1 = CreateTempImage(Path.Combine(input, "1.png"));
        var f2 = CreateTempImage(Path.Combine(input, "2.png"));
        _vm.AddFile(f1);
        _vm.AddFile(f2);

        await _vm.ApplyWatermarkAsync(output);

        Directory.GetFiles(output, "*_watermarked.jpg").Length.Should().Be(2);
        _vm.ProgressPercent.Should().Be(0);  // ApplyAsync 重置 progress
        _vm.IsProcessing.Should().BeFalse();
        _vm.StatusText.Should().Contain("完成");

        Directory.Delete(input, true);
        Directory.Delete(output, true);
    }

    [Fact]
    public async Task ApplyWatermarkAsync_SetsIsProcessingDuring()
    {
        var input = Path.Combine(Path.GetTempPath(), $"wf_proc_{Guid.NewGuid():N}");
        var output = Path.Combine(Path.GetTempPath(), $"wf_proc_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(input);
        CreateTempImage(Path.Combine(input, "1.png"));
        _vm.AddFile(Path.Combine(input, "1.png"));

        var processingSeen = false;
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsProcessing) && _vm.IsProcessing)
                processingSeen = true;
        };

        await _vm.ApplyWatermarkAsync(output);
        processingSeen.Should().BeTrue();
        _vm.IsProcessing.Should().BeFalse();  // finally 重置

        Directory.Delete(input, true);
        Directory.Delete(output, true);
    }

    [Fact]
    public async Task ApplyWatermarkAsync_DefaultOutputFolder_WhenEmpty()
    {
        var input = Path.Combine(Path.GetTempPath(), $"wf_defout_{Guid.NewGuid():N}");
        Directory.CreateDirectory(input);
        CreateTempImage(Path.Combine(input, "1.png"));
        _vm.AddFile(Path.Combine(input, "1.png"));

        await _vm.ApplyWatermarkAsync("");  // 空 → 用 Path.GetTempPath/wf_output

        _vm.StatusText.Should().Contain("完成");
        Directory.Delete(input, true);
        // wf_output 不一定能删（可能在 tmp 下共享）
    }

    // ============ 模板集成 ============

    [Fact]
    public void LoadTemplate_ReplacesConfig()
    {
        var originalName = _vm.Config.Name;
        var record = new TemplateRecord(
            1, "Test", new WatermarkConfig { Name = "Loaded" },
            DateTime.UtcNow, DateTime.UtcNow);
        _vm.LoadTemplate(record);
        _vm.Config.Name.Should().Be("Loaded");
        _vm.StatusText.Should().Contain("Test");
    }

    // ============ 裁剪（v0.3.2 per-image）============

    [Fact]
    public void SetCropRect_MatchingSelectedFile_UpdatesCurrentCropRect()
    {
        var path = CreateTempImage("crop-a.png");
        _vm.AddFile(path);
        _vm.SelectedFile = path;

        var crop = new CropRect(10, 20, 300, 200);
        _vm.SetCropRect(path, crop);

        _vm.CurrentCropRect.Should().Be(crop);
        _vm.GetCropRect(path).Should().Be(crop);
    }

    [Fact]
    public void SetCropRect_DifferentFile_DoesNotUpdateCurrentCropRect()
    {
        var pathA = CreateTempImage("crop-b-a.png");
        var pathB = CreateTempImage("crop-b-b.png");
        _vm.AddFile(pathA);
        _vm.AddFile(pathB);
        _vm.SelectedFile = pathA;

        var cropB = new CropRect(0, 0, 100, 100);
        _vm.SetCropRect(pathB, cropB);

        _vm.CurrentCropRect.Should().BeNull();        // 选中 A，B 的裁剪不影响当前
        _vm.GetCropRect(pathB).Should().Be(cropB);    // 但字典里有 B 的裁剪
    }

    [Fact]
    public void OnSelectedFileChanged_SyncsCurrentCropRectFromDictionary()
    {
        var pathA = CreateTempImage("crop-c-a.png");
        var pathB = CreateTempImage("crop-c-b.png");
        _vm.AddFile(pathA);
        _vm.AddFile(pathB);
        _vm.SelectedFile = pathA;

        var cropA = new CropRect(10, 10, 100, 100);
        var cropB = new CropRect(20, 20, 200, 200);
        _vm.SetCropRect(pathA, cropA);
        _vm.SetCropRect(pathB, cropB);

        _vm.SelectedFile = pathB;
        _vm.CurrentCropRect.Should().Be(cropB);

        _vm.SelectedFile = pathA;
        _vm.CurrentCropRect.Should().Be(cropA);
    }

    [Fact]
    public void ClearCropRect_RemovesFromDictionaryAndClearsCurrentIfWasSelected()
    {
        var path = CreateTempImage("crop-d.png");
        _vm.AddFile(path);
        _vm.SelectedFile = path;
        _vm.SetCropRect(path, new CropRect(0, 0, 50, 50));

        _vm.ClearCropRect(path);

        _vm.GetCropRect(path).Should().BeNull();
        _vm.CurrentCropRect.Should().BeNull();
    }

    [Fact]
    public void ClearAllCrops_EmptiesDictionaryAndResetsCurrent()
    {
        var pathA = CreateTempImage("crop-e-a.png");
        var pathB = CreateTempImage("crop-e-b.png");
        _vm.AddFile(pathA);
        _vm.AddFile(pathB);
        _vm.SelectedFile = pathA;
        _vm.SetCropRect(pathA, new CropRect(0, 0, 100, 100));
        _vm.SetCropRect(pathB, new CropRect(0, 0, 200, 200));

        _vm.ClearAllCrops();

        _vm.GetCropRect(pathA).Should().BeNull();
        _vm.GetCropRect(pathB).Should().BeNull();
        _vm.CurrentCropRect.Should().BeNull();
    }

    // ============ helpers ============

    private static string CreateTempImage(string fileName)
    {
        var path = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Path.GetTempPath(), fileName);
        using var img = new Image<Rgba32>(Configuration.Default, 50, 50);
        img.SaveAsPng(path);
        return path;
    }
}