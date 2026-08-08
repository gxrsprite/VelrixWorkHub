using VelrixWorkHub.Web.Localization;

namespace VelrixWorkHub.Domain.Tests;

public sealed class UiLocalizationServiceTests
{
    [Fact]
    public void SupportsChineseAndEnglishForModuleOverviewResources()
    {
        var service = new UiLocalizationService();

        Assert.Equal("CRM 经营看板", service.Text("CRM 经营看板"));
        Assert.Equal("原子多来源发运", service.Text("原子多来源发运"));
        Assert.Equal("FAT/SAT 验收", service.Text("FAT/SAT 验收"));
        Assert.Equal("售后设备档案", service.Text("售后设备档案"));
        Assert.Equal("设备标签", service.Text("设备标签"));
        Assert.Equal("售后安装工单", service.Text("售后安装工单"));
        Assert.Equal("维修", service.Text("维修"));
        service.SetLanguage("en-US");

        Assert.True(service.IsEnglish);
        Assert.Equal("CRM Business Dashboard", service.Text("CRM 经营看板"));
        Assert.Equal("ERP Operations Overview", service.Text("ERP 运营概览"));
        Assert.Equal("Workflow Workbench", service.Text("流程工作台"));
        Assert.Equal("License Operations Overview", service.Text("许可证运营概览"));
        Assert.Equal("Atomic multi-source shipment", service.Text("原子多来源发运"));
        Assert.Equal("Save multi-source shipment", service.Text("保存多来源发运"));
        Assert.Equal("FAT/SAT Acceptance", service.Text("FAT/SAT 验收"));
        Assert.Equal("Complete acceptance", service.Text("完成验收"));
        Assert.Equal("After-sales equipment archive", service.Text("售后设备档案"));
        Assert.Equal("Equipment label", service.Text("设备标签"));
        Assert.Equal("Print label", service.Text("打印标签"));
        Assert.Equal("After-sales installation work orders", service.Text("售后安装工单"));
        Assert.Equal("After-sales service work orders", service.Text("售后服务工单"));
        Assert.Equal("Create repair work order", service.Text("创建维修工单"));
        Assert.Equal("Repair spare-part consumption", service.Text("维修备件消耗"));
        Assert.Equal("Record part consumption", service.Text("登记备件消耗"));
        Assert.Equal("Repair spare-part inventory is insufficient. ", service.Text("维修备件库存不足。 "));
        Assert.Equal("Repair-site location is required.", service.Text("维修现场位置不能为空。"));
        Assert.Equal("The planned repair date cannot be before the work-order creation date.", service.Text("计划维修日期不能早于工单创建日期。"));
        Assert.Equal("Confirm completion", service.Text("确认完成"));
        Assert.Equal("Only shipped sales orders can create an after-sales equipment archive.", service.Text("只有已发运销售订单可以建立售后设备档案。"));
    }

    [Fact]
    public void RejectsUnsupportedLanguagesAndKeepsUnknownKeysReadable()
    {
        var service = new UiLocalizationService();
        var changed = 0;
        service.Changed += () => changed++;

        service.SetLanguage("fr-FR");
        Assert.False(service.IsEnglish);
        Assert.Equal("未登记资源", service.Text("未登记资源"));
        Assert.Equal(0, changed);

        service.SetLanguage("en-US");
        service.SetLanguage("en-GB");
        Assert.Equal(2, changed);
        Assert.False(service.IsEnglish);
        Assert.Equal("未登记资源", service.Text("未登记资源"));
    }
}
