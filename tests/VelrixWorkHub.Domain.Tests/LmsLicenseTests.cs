using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Application.Attachments;
using FreeSql.DataAnnotations;
using VelrixWorkHub.Infrastructure.Lms;

namespace VelrixWorkHub.Domain.Tests;

public sealed class LmsLicenseTests
{
    [Fact]
    public void CustomerReferenceImpact_BlocksCrmDeletionAndCountsLmsReferences()
    {
        var customer = new Customer("LMS 引用客户");
        var customers = new CustomerRepository();
        customers.Add(customer);
        var machines = new CustomerMachineRepository();
        var machine = new LmsCustomerMachine(customer.Id, "MACHINE-IMPACT", "Velrix", null, null, "{}", DateTime.Now);
        machines.Add(machine);
        var customerFeatures = new CustomerFeatureRepository();
        customerFeatures.Add(new LmsCustomerFeature(customer.Id, Guid.CreateVersion7(), null, null, "{}", DateTime.Now));
        var machineFeatures = new MachineFeatureRepository();
        machineFeatures.Add(new LmsMachineFeature(machine.Id, Guid.CreateVersion7(), null, null, "{}", DateTime.Now));
        var request = new LmsLicenseRequest("LMS-CUSTOMER-IMPACT", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now, customerId: customer.Id, customerMachineId: machine.Id);
        var licenses = new LicenseRepository(request);
        licenses.Add(new LmsLicenseAuthorization(request.Id, "LIC-CUSTOMER-IMPACT", "opaque", "Velrix", "[]", null, "{}", DateTime.Now, customerId: customer.Id, customerMachineId: machine.Id));
        var references = new LmsCustomerReferenceService(machines, customerFeatures, machineFeatures, licenses);
        var service = new CustomerService(customers, lmsReferences: references);

        var impact = references.GetImpact(customer.Id);

        Assert.Equal(1, impact.MachineReferenceCount);
        Assert.Equal(1, impact.CustomerFeatureReferenceCount);
        Assert.Equal(1, impact.MachineFeatureReferenceCount);
        Assert.Equal(1, impact.LicenseRequestReferenceCount);
        Assert.Equal(1, impact.AuthorizationReferenceCount);
        Assert.Throws<InvalidOperationException>(() => service.Remove(customer));
        Assert.Contains(customer, customers.List());
    }

    [Fact]
    public void ReplacementRequest_RequiresTargetOnlyForMachineChange_AndHasSubmitLifecycle()
    {
        var originalId = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new LmsLicenseReplacementRequest("LMS-REP-INVALID", originalId, LmsLicenseReplacementKind.MachineChange, null, "LIC-NEW", "opaque", null, "{}", "admin", "换机", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new LmsLicenseReplacementRequest("LMS-REP-INVALID-2", originalId, LmsLicenseReplacementKind.Renewal, Guid.CreateVersion7(), "LIC-NEW", "opaque", null, "{}", "admin", "续期", DateTime.Now));

        var request = new LmsLicenseReplacementRequest("LMS-REP-001", originalId, LmsLicenseReplacementKind.MachineChange, Guid.CreateVersion7(), "LIC-NEW", "opaque", DateTime.Today.AddYears(1), "{\"source\":\"replacement\"}", "admin", "客户更换设备", DateTime.Now);
        request.Submit();

        Assert.Equal(LmsLicenseReplacementRequestStatus.Submitted, request.Status);
        Assert.Contains("replacement", request.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => request.Submit());
    }

    [Fact]
    public void ReplacementRequestService_RequiresActiveSourceAndUniqueRequestNo()
    {
        var licenses = new LicenseRepository();
        var active = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-SOURCE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        var disabled = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-DISABLED", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        disabled.SetStatus(LmsLicenseStatus.Disabled);
        licenses.Add(active); licenses.Add(disabled);
        var requests = new ReplacementRequestRepository();
        var service = new LmsLicenseReplacementRequestService(requests, new LmsLicenseService(licenses));

        var submitted = service.Create("LMS-REP-SERVICE-01", active.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-REPLACEMENT-NEW", "opaque-new", DateTime.Today.AddYears(1), "{}", "admin", "正常续期");
        submitted.Submit();

        Assert.Throws<InvalidOperationException>(() => service.Create("LMS-REP-SERVICE-01", active.Id, LmsLicenseReplacementKind.Reissue, null, "LIC-REPLACEMENT-NEW-2", "opaque", DateTime.Today.AddYears(1), "{}", "admin", "重复申请"));
        Assert.Throws<InvalidOperationException>(() => service.Create("LMS-REP-SERVICE-RUNNING", active.Id, LmsLicenseReplacementKind.Reissue, null, "LIC-REPLACEMENT-NEW-4", "opaque", DateTime.Today.AddYears(1), "{}", "admin", "并行申请"));
        Assert.Throws<InvalidOperationException>(() => service.Create("LMS-REP-SERVICE-02", disabled.Id, LmsLicenseReplacementKind.Reissue, null, "LIC-REPLACEMENT-NEW-3", "opaque", DateTime.Today.AddYears(1), "{}", "admin", "停用授权"));
    }

    [Fact]
    public void ReplacementRequestService_RejectsCreationForAnotherApplicantsAuthorization()
    {
        var request = new LmsLicenseRequest("LMS-REQ-REPLACEMENT-SCOPE", "Alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        var licenses = new LicenseRepository(request);
        var authorization = new LmsLicenseAuthorization(request.Id, "LIC-REPLACEMENT-SCOPE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        licenses.Add(authorization);
        var service = new LmsLicenseReplacementRequestService(new ReplacementRequestRepository(), new LmsLicenseService(licenses));

        Assert.Throws<InvalidOperationException>(() => service.Create("LMS-REP-SCOPE-BOB", authorization.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-SCOPE-BOB", "opaque", DateTime.Today.AddYears(1), "{}", "Bob", "越权测试"));
        var own = service.Create("LMS-REP-SCOPE-ALICE", authorization.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-SCOPE-ALICE", "opaque", DateTime.Today.AddYears(1), "{}", "alice", "本人续期");

        Assert.Equal("alice", own.Applicant);
    }

    [Fact]
    public void ReplacementRequestSubmission_RejectsDraftThatWouldCreateParallelApproval()
    {
        var licenses = new LicenseRepository();
        var original = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-PARALLEL-SOURCE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        licenses.Add(original);
        var requests = new ReplacementRequestRepository();
        var running = new LmsLicenseReplacementRequest("LMS-REP-PARALLEL-RUNNING", original.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-REPLACEMENT-PARALLEL-RUNNING", "opaque-running", DateTime.Today.AddYears(1), "{}", "admin", "审批中", DateTime.Now);
        running.Submit();
        var draft = new LmsLicenseReplacementRequest("LMS-REP-PARALLEL-DRAFT", original.Id, LmsLicenseReplacementKind.Reissue, null, "LIC-REPLACEMENT-PARALLEL-DRAFT", "opaque-draft", DateTime.Today.AddYears(1), "{}", "admin", "历史草稿", DateTime.Now);
        requests.Add(running);
        requests.Add(draft);
        var service = new LmsLicenseReplacementRequestService(requests, new LmsLicenseService(licenses));

        Assert.Throws<InvalidOperationException>(() => service.SubmitAndStartWorkflow(draft, "admin"));
        Assert.Equal(LmsLicenseReplacementRequestStatus.Draft, draft.Status);
    }

    [Fact]
    public void ReplacementRequestWorkflowAction_ApprovesAndExecutesReplacementWithActualActor()
    {
        var licenses = new LicenseRepository();
        var original = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-WF-OLD", "opaque-old", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        licenses.Add(original);
        var requests = new ReplacementRequestRepository();
        var request = new LmsLicenseReplacementRequest("LMS-REP-WF-01", original.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-REPLACEMENT-WF-NEW", "opaque-new", DateTime.Today.AddYears(1), "{}", "applicant", "合同续期", DateTime.Now);
        request.Submit();
        requests.Add(request);
        var licenseService = new LmsLicenseService(licenses);
        var handler = new LmsLicenseReplacementWorkflowActionHandler(requests, new LmsServiceProvider(licenseService));
        var action = new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(LmsLicenseReplacementRequest.Status), nameof(LmsLicenseReplacementRequestStatus.Approved));

        handler.Execute(new WorkflowActionContext(CreateInstance(request.Id), WorkflowActionTrigger.Approved, "批准", "finance"), action);

        var replacement = licenses.ListAuthorizations().Single(x => x.Id != original.Id);
        Assert.Equal(LmsLicenseReplacementRequestStatus.Approved, request.Status);
        Assert.Equal(LmsLicenseStatus.Disabled, original.Status);
        Assert.Equal(LmsLicenseReplacementKind.Renewal, replacement.ReplacementKind);
        Assert.Equal(request.Id, replacement.ReplacementRequestId);
        Assert.Single(licenseService.ListLifecycle(original.Id));
    }

    [Fact]
    public void ReplacementRequestResubmitAfterWithdrawal_ReopensRequest_AndLinksNewWorkflowInstance()
    {
        var licenses = new LicenseRepository();
        var original = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-RESUBMIT-SOURCE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        licenses.Add(original);
        var request = new LmsLicenseReplacementRequest("LMS-REP-RESUBMIT-01", original.Id, LmsLicenseReplacementKind.Renewal, null, "LIC-REPLACEMENT-RESUBMIT-NEW", "opaque-new", DateTime.Today.AddYears(1), "{}", "admin", "续期", DateTime.Now);
        request.Submit();
        var definition = CreateApprovalDefinition(WorkflowBindingCodes.LmsLicenseReplacementApproval);
        var definitions = new DefinitionRepository(definition);
        var instances = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var previous = instanceService.Start(definition, nameof(LmsLicenseReplacementRequest), request.Id, startedBy: "admin");
        instanceService.Cancel(previous);
        var service = new LmsLicenseReplacementRequestService(new ReplacementRequestRepositoryWith(request), new LmsLicenseService(licenses), new WorkflowBindingService(new WorkflowDefinitionService(definitions), instanceService));

        service.ResubmitAfterWithdrawal(request, "admin");

        var current = Assert.Single(instances.List(nameof(LmsLicenseReplacementRequest), request.Id, WorkflowInstanceStatus.Running));
        Assert.Equal(previous.Id, current.PreviousInstanceId);
        Assert.Equal(LmsLicenseReplacementRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void ReplacementRequestSubmitAfterRejection_ReopensRequest_AndLinksNewWorkflowInstance()
    {
        var licenses = new LicenseRepository();
        var original = new LmsLicenseAuthorization(null, "LIC-REPLACEMENT-REJECTED-SOURCE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        licenses.Add(original);
        var request = new LmsLicenseReplacementRequest("LMS-REP-REJECTED-01", original.Id, LmsLicenseReplacementKind.Reissue, null, "LIC-REPLACEMENT-REJECTED-NEW", "opaque-new", DateTime.Today.AddYears(1), "{}", "admin", "需重新签发", DateTime.Now);
        request.SetStatus(LmsLicenseReplacementRequestStatus.Rejected);
        var definition = CreateApprovalDefinition(WorkflowBindingCodes.LmsLicenseReplacementApproval);
        var definitions = new DefinitionRepository(definition);
        var instances = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var previous = instanceService.Start(definition, nameof(LmsLicenseReplacementRequest), request.Id, startedBy: "admin");
        instanceService.Reject(previous);
        var service = new LmsLicenseReplacementRequestService(new ReplacementRequestRepositoryWith(request), new LmsLicenseService(licenses), new WorkflowBindingService(new WorkflowDefinitionService(definitions), instanceService));

        service.SubmitAndStartWorkflow(request, "admin");

        var current = Assert.Single(instances.List(nameof(LmsLicenseReplacementRequest), request.Id, WorkflowInstanceStatus.Running));
        Assert.Equal(previous.Id, current.PreviousInstanceId);
        Assert.Equal(LmsLicenseReplacementRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void AuthorizationRecord_UsesUniqueOrderedPositionsAndStringEnums()
    {
        var columns = typeof(LmsLicenseAuthorizationRecord).GetProperties()
            .Select(property => (Property: property, Column: property.GetCustomAttributes(typeof(ColumnAttribute), inherit: false).Cast<ColumnAttribute>().Single()))
            .ToArray();

        Assert.Equal(Enumerable.Range(1, columns.Length).Select(x => (short)x), columns.Select(x => x.Column.Position).OrderBy(x => x));
        Assert.All(columns.Where(x => x.Property.PropertyType is var type && (type == typeof(LmsLicenseStatus) || type == typeof(LmsLicenseReplacementKind?))), x =>
        {
            Assert.Equal(typeof(string), x.Column.MapType);
            Assert.Equal(50, x.Column.StringLength);
        });
    }

    [Fact]
    public void Feature_RequiresUniqueCode_AndSupportsOtherInfoAndDisable()
    {
        var repository = new FeatureRepository();
        var service = new LmsFeatureService(repository);
        var feature = service.Create("REPORT", "报表中心", "报表能力", "{\"tier\":\"pro\"}");
        Assert.Contains("tier", feature.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create("report", "重复", null, "{}"));
        service.SetStatus(feature, LmsFeatureStatus.Disabled);
        Assert.Empty(service.List(includeDisabled: false));
    }

    [Fact]
    public void FeatureVersion_RequiresActiveFeatureAndUniqueVersion_AndRetainsLevelScope()
    {
        var features = new FeatureRepository();
        var featureService = new LmsFeatureService(features);
        var versions = new FeatureVersionRepository();
        var service = new LmsFeatureVersionService(versions, featureService);
        var feature = featureService.Create("REPORT", "报表中心", null, "{}");

        var version = service.Create(feature.Id, "1.0", LmsFeatureLevel.Advanced, LmsFeatureScope.Machine, "{\"edition\":\"pro\"}");

        Assert.Equal(LmsFeatureLevel.Advanced, version.Level);
        Assert.Equal(LmsFeatureScope.Machine, version.Scope);
        Assert.Contains("edition", version.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(feature.Id, "1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Customer, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(Guid.CreateVersion7(), "1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Customer, "{}"));
        featureService.SetStatus(feature, LmsFeatureStatus.Disabled);
        Assert.Throws<InvalidOperationException>(() => service.Create(feature.Id, "2.0", LmsFeatureLevel.Basic, LmsFeatureScope.Customer, "{}"));
        service.SetStatus(version, LmsFeatureVersionStatus.Disabled);
        Assert.Empty(service.List(feature.Id, includeDisabled: false));
    }

    [Fact]
    public void CustomerMachine_UsesCrmCustomerReference_AndRejectsInactiveCustomerOrProduct()
    {
        var customers = new CustomerRepository();
        var customerService = new CustomerService(customers);
        var activeCustomer = customerService.Create("客户 A", null, null, null, null);
        var inactiveCustomer = customerService.Create("客户 B", null, null, null, null);
        customerService.SetActive(inactiveCustomer, false);
        var productService = new LmsLicenseProductService(new ProductRepository());
        var product = productService.Create("LMS-PRO", "专业版", null, "{}");
        var service = new LmsCustomerMachineService(new CustomerMachineRepository(), customerService, productService);

        var machine = service.Create(activeCustomer.Id, "MACHINE-001", "专业版", "M1", "Production", "{\"ip\":\"10.0.0.1\"}");

        Assert.Equal(activeCustomer.Id, machine.CustomerId);
        Assert.Contains("ip", machine.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(activeCustomer.Id, "machine-001", "专业版", null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(inactiveCustomer.Id, "MACHINE-002", "专业版", null, null, "{}"));
        productService.SetStatus(product, LmsLicenseProductStatus.Disabled);
        Assert.Throws<InvalidOperationException>(() => service.Create(activeCustomer.Id, "MACHINE-003", "专业版", null, null, "{}"));
        service.SetStatus(machine, LmsCustomerMachineStatus.Disabled);
        Assert.Empty(service.List(activeCustomer.Id, includeDisabled: false));
    }

    [Fact]
    public void CustomerFeature_RequiresActiveCustomerScopedVersion_AndPreservesExpiry()
    {
        var customerService = new CustomerService(new CustomerRepository());
        var customer = customerService.Create("客户 A", null, null, null, null);
        var featureService = new LmsFeatureService(new FeatureRepository());
        var feature = featureService.Create("REPORT", "报表", null, "{}");
        var versionService = new LmsFeatureVersionService(new FeatureVersionRepository(), featureService);
        var customerVersion = versionService.Create(feature.Id, "1.0", LmsFeatureLevel.Intermediate, LmsFeatureScope.Customer, "{}");
        var machineVersion = versionService.Create(feature.Id, "2.0", LmsFeatureLevel.Advanced, LmsFeatureScope.Machine, "{}");
        var service = new LmsCustomerFeatureService(new CustomerFeatureRepository(), customerService, versionService);

        var grant = service.Create(customer.Id, customerVersion.Id, DateTime.Today.AddYears(1), "合同授权", "{\"source\":\"contract\"}");

        Assert.Equal(customerVersion.Id, grant.FeatureVersionId);
        Assert.Contains("source", grant.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(customer.Id, customerVersion.Id, null, null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.Create(customer.Id, machineVersion.Id, null, null, "{}"));
        versionService.SetStatus(customerVersion, LmsFeatureVersionStatus.Disabled);
        Assert.Throws<InvalidOperationException>(() => service.Edit(grant, null, "更新", "{}"));
        service.SetStatus(grant, LmsCustomerFeatureStatus.Disabled);
        Assert.Empty(service.List(customer.Id, includeDisabled: false));
    }

    [Fact]
    public void MachineFeature_RequiresMatchingCustomerBaseline_AndCannotEscalateLevel()
    {
        var customerService = new CustomerService(new CustomerRepository());
        var customer = customerService.Create("客户 A", null, null, null, null);
        var productService = new LmsLicenseProductService(new ProductRepository());
        productService.Create("LMS-PRO", "专业版", null, "{}");
        var machines = new LmsCustomerMachineService(new CustomerMachineRepository(), customerService, productService);
        var machine = machines.Create(customer.Id, "MACHINE-001", "专业版", "X100", "Production", "{}");
        var features = new LmsFeatureService(new FeatureRepository());
        var feature = features.Create("REPORT", "报表", null, "{}");
        var versions = new LmsFeatureVersionService(new FeatureVersionRepository(), features);
        var customerVersion = versions.Create(feature.Id, "C-1.0", LmsFeatureLevel.Intermediate, LmsFeatureScope.Customer, "{}");
        var machineBasic = versions.Create(feature.Id, "M-1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Machine, "{}");
        var machineAdvanced = versions.Create(feature.Id, "2.0", LmsFeatureLevel.Advanced, LmsFeatureScope.Machine, "{}");
        var customerFeatures = new LmsCustomerFeatureService(new CustomerFeatureRepository(), customerService, versions);
        var service = new LmsMachineFeatureService(new MachineFeatureRepository(), machines, customerFeatures, versions);

        Assert.Throws<InvalidOperationException>(() => service.Create(machine.Id, machineBasic.Id, null, null, "{}"));
        customerFeatures.Create(customer.Id, customerVersion.Id, null, null, "{}");
        var grant = service.Create(machine.Id, machineBasic.Id, DateTime.Today.AddMonths(1), "机台限定", "{\"mode\":\"limited\"}");
        Assert.Contains("limited", grant.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Create(machine.Id, machineAdvanced.Id, null, null, "{}"));
        service.SetStatus(grant, LmsMachineFeatureStatus.Disabled);
        Assert.Empty(service.List(machine.Id, includeDisabled: false));
    }

    [Fact]
    public void MachineRequest_StoresReferences_AndOnlyAcceptsEnabledMachineFeatures()
    {
        var customerService = new CustomerService(new CustomerRepository());
        var customer = customerService.Create("客户 A", null, null, null, null);
        var contacts = new CustomerContactService(new ContactRepository());
        var contact = contacts.Create(customer.Id, "张三", null, null, null, false);
        var productService = new LmsLicenseProductService(new ProductRepository());
        productService.Create("LMS-PRO", "专业版", null, "{}");
        var machines = new LmsCustomerMachineService(new CustomerMachineRepository(), customerService, productService);
        var machine = machines.Create(customer.Id, "MACHINE-001", "专业版", "X100", "Production", "{}");
        var features = new LmsFeatureService(new FeatureRepository());
        var feature = features.Create("REPORT", "报表", null, "{}");
        var versions = new LmsFeatureVersionService(new FeatureVersionRepository(), features);
        var customerVersion = versions.Create(feature.Id, "C-1.0", LmsFeatureLevel.Intermediate, LmsFeatureScope.Customer, "{}");
        var machineVersion = versions.Create(feature.Id, "M-1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Machine, "{}");
        var customerFeatures = new LmsCustomerFeatureService(new CustomerFeatureRepository(), customerService, versions);
        customerFeatures.Create(customer.Id, customerVersion.Id, null, null, "{}");
        var machineFeatures = new LmsMachineFeatureService(new MachineFeatureRepository(), machines, customerFeatures, versions);
        machineFeatures.Create(machine.Id, machineVersion.Id, null, null, "{}");
        var repository = new LicenseRepository();
        var service = new LmsLicenseService(repository, products: productService, customers: customerService, machines: machines, machineFeatures: machineFeatures, featureVersions: versions, contacts: contacts);

        var scopeError = Assert.Throws<InvalidOperationException>(() => service.CreateMachineRequest("LMS-REQ-MACHINE-SCOPE", "alice", "bob", customer.Id, null, machine.Id, "专业版", $"[\"{machineVersion.Id}\"]", null, "{}"));
        Assert.Contains("当前登录用户身份", scopeError.Message);
        var administratorRequest = service.CreateMachineRequest("LMS-REQ-MACHINE-ADMIN", "alice", "admin", customer.Id, null, machine.Id, "专业版", $"[\"{machineVersion.Id}\"]", null, "{}", isAdministrator: true);
        Assert.Equal("alice", administratorRequest.Applicant);
        var request = service.CreateMachineRequest("LMS-REQ-MACHINE-01", "admin", "admin", customer.Id, contact.Id, machine.Id, "专业版", $"[\"{machineVersion.Id}\"]", DateTime.Today.AddYears(1), "{\"channel\":\"machine\"}", 15);

        Assert.Equal(customer.Id, request.CustomerId);
        Assert.Equal(machine.Id, request.CustomerMachineId);
        Assert.Equal(contact.Id, request.ContactId);
        Assert.Equal("X100", request.Model);
        Assert.Equal("Production", request.Environment);
        Assert.Equal(15, request.GracePeriodDays);
        Assert.Contains(machineVersion.Id.ToString(), request.FeatureVersionIdsJson);
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        var authorization = service.RegisterExternalLicenseFromRequest(request.Id, "LIC-MACHINE-01", "opaque-license", null, "{}", "admin");
        Assert.Equal(request.CustomerId, authorization.CustomerId);
        Assert.Equal(request.CustomerMachineId, authorization.CustomerMachineId);
        Assert.Equal(request.ContactId, authorization.ContactId);
        Assert.Equal(request.FeatureVersionIdsJson, authorization.FeatureVersionIdsJson);
        Assert.Equal(request.Model, authorization.Model);
        Assert.Equal(request.Environment, authorization.Environment);
        Assert.Equal(request.GracePeriodDays, authorization.GracePeriodDays);
        Assert.Throws<InvalidOperationException>(() => service.CreateMachineRequest("LMS-REQ-MACHINE-CONFLICT", "admin", "admin", customer.Id, null, machine.Id, "专业版", $"[\"{machineVersion.Id}\"]", null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.CreateMachineRequest("LMS-REQ-MACHINE-02", "admin", "admin", customer.Id, null, machine.Id, "专业版", "[]", null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.CreateMachineRequest("LMS-REQ-MACHINE-03", "admin", "admin", customer.Id, null, machine.Id, "其他产品", $"[\"{machineVersion.Id}\"]", null, "{}"));
        Assert.Throws<InvalidOperationException>(() => service.CreateMachineRequest("LMS-REQ-MACHINE-04", "admin", "admin", customer.Id, Guid.CreateVersion7(), machine.Id, "专业版", $"[\"{machineVersion.Id}\"]", null, "{}"));
    }
    [Fact]
    public void LicenseProduct_DisabledProductCannotCreateNewRequest_AndCodeIsUnique()
    {
        var products = new ProductRepository();
        var productService = new LmsLicenseProductService(products);
        var product = productService.Create("LMS-PRO-01", "专业版", null, "{}");
        Assert.Throws<InvalidOperationException>(() => productService.Create("lms-pro-01", "重复", null, "{}"));
        productService.Edit(product, "LMS-PRO-01", "专业版 2026", "更新说明", "{\"tier\":\"pro\"}");
        Assert.Equal("专业版 2026", product.Name);
        Assert.Contains("tier", product.OtherInfo);
        var licenses = new LmsLicenseService(new LicenseRepository(), products: productService);
        licenses.CreateRequest("LMS-REQ-PRODUCT-01", "admin", "admin", "专业版 2026", null, "[]", null, "{}");
        productService.SetStatus(product, LmsLicenseProductStatus.Disabled);
        Assert.Throws<InvalidOperationException>(() => licenses.CreateRequest("LMS-REQ-PRODUCT-02", "admin", "admin", "专业版 2026", null, "[]", null, "{}"));
    }

    [Fact]
    public void LegacyRequestCreation_RejectsForgedApplicant_UnlessAdministrator()
    {
        var service = new LmsLicenseService(new LicenseRepository());

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateRequest("LMS-REQ-CREATE-SCOPE", "alice", "bob", "Velrix", null, "[]", null, "{}"));
        Assert.Contains("当前登录用户身份", error.Message);
        Assert.Empty(service.ListRequests());

        var request = service.CreateRequest("LMS-REQ-CREATE-ADMIN", "alice", "admin", "Velrix", null, "[]", null, "{}", isAdministrator: true);
        Assert.Equal("alice", request.Applicant);
    }

    [Fact]
    public void LicenseRequest_StoresFeatureAndOtherInfoJson_AndSubmits()
    {
        var request = new LmsLicenseRequest("LMS-REQ-001", "admin", "Velrix", "客户 A", "[\"基础\",\"报表\"]", DateTime.Today.AddYears(1), "{\"region\":\"CN\"}", DateTime.Now);
        request.Submit();
        Assert.Equal(LmsLicenseRequestStatus.Submitted, request.Status);
        Assert.Contains("基础", request.FeaturesJson);
        Assert.Contains("region", request.OtherInfo);
    }

    [Fact]
    public void LicenseApplication_RejectsExpiredRequestAndAuthorizationExpiry()
    {
        var repository = new LicenseRepository();
        var products = new LmsLicenseProductService(new ProductRepository());
        products.Create("LMS-EXPIRY", "到期校验产品", null, "{}");
        var service = new LmsLicenseService(repository, products: products);

        Assert.Throws<InvalidOperationException>(() => service.CreateRequest(
            "LMS-REQ-EXPIRED", "admin", "admin", "到期校验产品", null, "[]", DateTime.Now, "{}"));

        var request = service.CreateRequest(
            "LMS-REQ-FUTURE", "admin", "admin", "到期校验产品", null, "[]", DateTime.Now.AddDays(1), "{}");
        request.SetStatus(LmsLicenseRequestStatus.Approved);

        Assert.Throws<InvalidOperationException>(() => service.RegisterExternalLicenseFromRequest(
            request.Id, "LIC-EXPIRED-APP", "opaque", DateTime.Now, "{}", "admin"));

        var authorization = service.RegisterExternalLicenseFromRequest(
            request.Id, "LIC-FUTURE-APP", "opaque", DateTime.Now.AddDays(1), "{}", "admin");
        Assert.Equal(LmsLicenseStatus.Active, authorization.Status);
    }

    [Fact]
    public void ExternalAuthorizationRegistration_RejectsAnotherApplicantsRequest_UnlessAdministrator()
    {
        var request = new LmsLicenseRequest("LMS-REQ-AUTH-SCOPE", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        var repository = new LicenseRepository(request);
        var service = new LmsLicenseService(repository);

        var error = Assert.Throws<InvalidOperationException>(() => service.RegisterExternalLicenseFromRequest(request.Id, "LIC-AUTH-SCOPE-BOB", "opaque", null, "{}", "bob"));
        Assert.Contains("无权", error.Message);
        Assert.Empty(repository.ListAuthorizations());

        var authorization = service.RegisterExternalLicenseFromRequest(request.Id, "LIC-AUTH-SCOPE-ADMIN", "opaque", null, "{}", "admin", isAdministrator: true);
        Assert.Equal(request.Id, authorization.RequestId);
    }

    [Fact]
    public void LicenseAuthorization_RequiresOpaqueExternalLicenseAndJsonExtensions()
    {
        Assert.Throws<ArgumentException>(() => new LmsLicenseAuthorization(null, "LIC-001", "", "Velrix", "[]", null, "{}", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new LmsLicenseAuthorization(null, "LIC-001", "external-value", "Velrix", "{}", null, "{}", DateTime.Now));
        Assert.Throws<ArgumentException>(() => new LmsLicenseAuthorization(null, "LIC-001", "external-value", "Velrix", "[]", null, "[]", DateTime.Now));
    }

    [Fact]
    public void Authorization_UsesDerivedExpiryStatus_AndActiveQueryExcludesExpiredItems()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var expired = new LmsLicenseAuthorization(null, "LIC-EXPIRED", "opaque-1", "Velrix", "[]", now.AddMinutes(-1), "{}", now.AddDays(-2));
        var active = new LmsLicenseAuthorization(null, "LIC-ACTIVE", "opaque-2", "Velrix", "[]", now.AddDays(1), "{}", now.AddDays(-1));
        var repository = new LicenseRepository();
        repository.Add(expired);
        repository.Add(active);

        Assert.Equal(LmsLicenseStatus.Expired, expired.GetEffectiveStatus(now));
        Assert.Equal(LmsLicenseStatus.Active, active.GetEffectiveStatus(now));
        Assert.Equal([active], new LmsLicenseService(repository).ListAuthorizations(includeInactive: false, now));
    }

    [Fact]
    public void Authorization_GracePeriod_ExtendsEffectiveExpiryWithoutMutatingStoredExpiry()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var authorization = new LmsLicenseAuthorization(null, "LIC-GRACE", "opaque", "Velrix", "[]", now.AddDays(-1), "{}", now.AddDays(-10), gracePeriodDays: 7);

        Assert.Equal(now.AddDays(6), authorization.EffectiveExpiresAt);
        Assert.True(authorization.IsWithinGracePeriod(now));
        Assert.Equal(LmsLicenseStatus.Active, authorization.GetEffectiveStatus(now));
        Assert.Equal(LmsLicenseStatus.Expired, authorization.GetEffectiveStatus(now.AddDays(7)));
        Assert.Equal(now.AddDays(-1), authorization.ExpiresAt);
    }

    [Fact]
    public void AuthorizationLifecycle_DisableEnableAndRevoke_AreAuditedAndGuarded()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var authorization = new LmsLicenseAuthorization(null, "LIC-LIFECYCLE", "opaque", "Velrix", "[]", now.AddDays(1), "{}", now);
        var repository = new LicenseRepository();
        repository.Add(authorization);
        var service = new LmsLicenseService(repository);

        Assert.Throws<ArgumentException>(() => service.DisableAuthorization(authorization, "admin", "", isAdministrator: true));
        service.DisableAuthorization(authorization, "admin", "客户暂停使用", now.AddMinutes(1), isAdministrator: true);
        service.EnableAuthorization(authorization, "admin", "客户恢复使用", now.AddMinutes(2), isAdministrator: true);
        service.RevokeAuthorization(authorization, "admin", "合同终止", now.AddMinutes(3), isAdministrator: true);

        Assert.Equal(LmsLicenseStatus.Revoked, authorization.Status);
        Assert.Equal([LmsLicenseLifecycleAction.Revoked, LmsLicenseLifecycleAction.Enabled, LmsLicenseLifecycleAction.Disabled], service.ListLifecycle(authorization.Id).Select(x => x.Action));
        Assert.Throws<InvalidOperationException>(() => service.EnableAuthorization(authorization, "admin", "不允许恢复", now.AddMinutes(4), isAdministrator: true));
        Assert.Throws<InvalidOperationException>(() => service.RevokeAuthorization(authorization, "admin", "重复作废", now.AddMinutes(4), isAdministrator: true));
    }

    [Fact]
    public void AuthorizationLifecycle_ExpiredAuthorization_CannotBeEnabled()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var authorization = new LmsLicenseAuthorization(null, "LIC-EXPIRED-ENABLE", "opaque", "Velrix", "[]", now.AddMinutes(-1), "{}", now.AddDays(-2));
        authorization.SetStatus(LmsLicenseStatus.Disabled);

        Assert.Throws<InvalidOperationException>(() => authorization.Enable("admin", "尝试恢复", now));
    }

    [Fact]
    public void AuthorizationLifecycle_RejectsAnotherApplicantsAuthorization_UnlessAdministrator()
    {
        var request = new LmsLicenseRequest("LMS-REQ-LIFECYCLE-SCOPE", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var authorization = new LmsLicenseAuthorization(request.Id, "LIC-LIFECYCLE-SCOPE", "opaque", "Velrix", "[]", DateTime.Today.AddDays(30), "{}", DateTime.Now);
        var repository = new LicenseRepository(request);
        repository.Add(authorization);
        var service = new LmsLicenseService(repository);

        var error = Assert.Throws<InvalidOperationException>(() => service.DisableAuthorization(authorization, "bob", "越权停用"));
        Assert.Contains("无权", error.Message);
        Assert.Equal(LmsLicenseStatus.Active, authorization.Status);

        service.DisableAuthorization(authorization, "admin", "管理员停用", isAdministrator: true);
        Assert.Equal(LmsLicenseStatus.Disabled, authorization.Status);
    }

    [Fact]
    public void AuthorizationReplacement_DisablesOriginalAndPreservesSourceChain()
    {
        var original = new LmsLicenseAuthorization(null, "LIC-OLD", "opaque-old", "Velrix", "[]", DateTime.Today.AddDays(1), "{}", DateTime.Now);
        var repository = new LicenseRepository();
        repository.Add(original);
        var service = new LmsLicenseService(repository);

        var replacement = service.ReplaceAuthorization(original, LmsLicenseReplacementKind.Renewal, "LIC-NEW", "opaque-new", DateTime.Today.AddYears(1), "{\"reason\":\"renewal\"}", "admin", "续期替代旧授权");

        Assert.Equal(LmsLicenseStatus.Disabled, original.Status);
        Assert.Equal(original.Id, replacement.SupersedesAuthorizationId);
        Assert.Equal(LmsLicenseReplacementKind.Renewal, replacement.ReplacementKind);
        Assert.Contains("renewal", replacement.OtherInfo);
        Assert.Single(service.ListLifecycle(original.Id));
        Assert.Throws<InvalidOperationException>(() => service.ReplaceAuthorization(replacement, LmsLicenseReplacementKind.Reissue, "LIC-INVALID", "opaque-invalid", DateTime.Now.AddMinutes(-1), "{}", "admin", "到期时间非法"));
        Assert.Throws<InvalidOperationException>(() => service.ReplaceAuthorization(replacement, LmsLicenseReplacementKind.MachineChange, "LIC-INVALID-MOVE", "opaque-invalid", DateTime.Today.AddYears(1), "{}", "admin", "错误换机入口"));
    }

    [Fact]
    public void AuthorizationMachineChange_MovesActiveLicenseToCompatibleMachine_AndRejectsInvalidTargets()
    {
        var customerService = new CustomerService(new CustomerRepository());
        var customer = customerService.Create("客户 A", null, null, null, null);
        var otherCustomer = customerService.Create("客户 B", null, null, null, null);
        var products = new LmsLicenseProductService(new ProductRepository());
        products.Create("LMS-PRO", "专业版", null, "{}");
        products.Create("LMS-OTHER", "其他产品", null, "{}");
        var machines = new LmsCustomerMachineService(new CustomerMachineRepository(), customerService, products);
        var sourceMachine = machines.Create(customer.Id, "MACHINE-SOURCE", "专业版", null, null, "{}");
        var targetMachine = machines.Create(customer.Id, "MACHINE-TARGET", "专业版", null, null, "{}");
        var otherCustomerMachine = machines.Create(otherCustomer.Id, "MACHINE-OTHER-CUSTOMER", "专业版", null, null, "{}");
        var otherProductMachine = machines.Create(customer.Id, "MACHINE-OTHER-PRODUCT", "其他产品", null, null, "{}");
        var features = new LmsFeatureService(new FeatureRepository());
        var feature = features.Create("REPORT", "报表", null, "{}");
        var versions = new LmsFeatureVersionService(new FeatureVersionRepository(), features);
        var customerVersion = versions.Create(feature.Id, "C-1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Customer, "{}");
        var machineVersion = versions.Create(feature.Id, "M-1.0", LmsFeatureLevel.Basic, LmsFeatureScope.Machine, "{}");
        var customerFeatures = new LmsCustomerFeatureService(new CustomerFeatureRepository(), customerService, versions);
        customerFeatures.Create(customer.Id, customerVersion.Id, null, null, "{}");
        var machineFeatures = new LmsMachineFeatureService(new MachineFeatureRepository(), machines, customerFeatures, versions);
        machineFeatures.Create(sourceMachine.Id, machineVersion.Id, null, null, "{}");
        machineFeatures.Create(targetMachine.Id, machineVersion.Id, null, null, "{}");
        var repository = new LicenseRepository();
        var service = new LmsLicenseService(repository, products: products, customers: customerService, machines: machines, machineFeatures: machineFeatures, featureVersions: versions);
        var request = service.CreateMachineRequest("LMS-REQ-MOVE", "admin", "admin", customer.Id, null, sourceMachine.Id, "专业版", $"[\"{machineVersion.Id}\"]", null, "{}");
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        var original = service.RegisterExternalLicenseFromRequest(request.Id, "LIC-MOVE-OLD", "opaque-old", DateTime.Today.AddDays(30), "{}", "admin");

        Assert.Throws<InvalidOperationException>(() => service.ChangeMachine(original, sourceMachine.Id, "LIC-MOVE-SAME", "opaque", null, "{}", "admin", "同机"));
        Assert.Throws<InvalidOperationException>(() => service.ChangeMachine(original, otherCustomerMachine.Id, "LIC-MOVE-CUSTOMER", "opaque", null, "{}", "admin", "跨客户"));
        Assert.Throws<InvalidOperationException>(() => service.ChangeMachine(original, otherProductMachine.Id, "LIC-MOVE-PRODUCT", "opaque", null, "{}", "admin", "跨产品"));

        var replacement = service.ChangeMachine(original, targetMachine.Id, "LIC-MOVE-NEW", "opaque-new", DateTime.Today.AddYears(1), "{\"reason\":\"machine-change\"}", "admin", "设备更换");

        Assert.Equal(LmsLicenseStatus.Disabled, original.Status);
        Assert.Equal(targetMachine.Id, replacement.CustomerMachineId);
        Assert.Equal(original.Id, replacement.SupersedesAuthorizationId);
        Assert.Equal(LmsLicenseReplacementKind.MachineChange, replacement.ReplacementKind);
        Assert.Single(service.ListLifecycle(original.Id));
    }

    [Fact]
    public void ExpiryReminderScan_PublishesExpiringAndExpiredOnce_AndSkipsInactiveOrUnlinkedLicenses()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var repository = new LicenseRepository();
        var request = new LmsLicenseRequest("LMS-REQ-REMINDER", "Applicant", "Velrix", null, "[]", null, "{}", now);
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        repository.Add(request);
        var expiring = new LmsLicenseAuthorization(request.Id, "LIC-EXPIRING", "opaque-1", "Velrix", "[]", now.AddDays(30), "{}", now);
        var expired = new LmsLicenseAuthorization(request.Id, "LIC-EXPIRED", "opaque-2", "Velrix", "[]", now.AddMinutes(-1), "{}", now.AddDays(-40));
        var disabled = new LmsLicenseAuthorization(request.Id, "LIC-DISABLED", "opaque-3", "Velrix", "[]", now.AddDays(1), "{}", now);
        disabled.SetStatus(LmsLicenseStatus.Disabled);
        var unlinked = new LmsLicenseAuthorization(null, "LIC-UNLINKED", "opaque-4", "Velrix", "[]", now.AddDays(1), "{}", now);
        repository.Add(expiring); repository.Add(expired); repository.Add(disabled); repository.Add(unlinked);
        var notifications = new NotificationRepository();
        var scanner = new LmsLicenseExpiryReminderService(repository, new NotificationService(notifications));

        var first = scanner.Scan(now);
        var second = scanner.Scan(now.AddMinutes(5));

        Assert.Equal(1, first.ExpiringNotifications);
        Assert.Equal(1, first.ExpiredNotifications);
        Assert.Equal(2, first.SkippedAuthorizations);
        Assert.Equal(2, second.SkippedAuthorizations);
        Assert.Equal(2, notifications.Items.Count);
        Assert.All(notifications.Items, x => Assert.Equal("applicant", x.Recipient));
        Assert.Contains(notifications.Items, x => x.Title == "许可证即将到期" && x.Href == $"/Lms/License?requestId={request.Id}");
        Assert.Contains(notifications.Items, x => x.Title == "许可证已到期");
    }

    [Fact]
    public void ExpiryReminderScan_PublishesGracePeriodBeforeFinalExpiry()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var repository = new LicenseRepository();
        var request = new LmsLicenseRequest("LMS-REQ-GRACE", "Applicant", "Velrix", null, "[]", null, "{}", now);
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        repository.Add(request);
        repository.Add(new LmsLicenseAuthorization(request.Id, "LIC-GRACE-REMINDER", "opaque", "Velrix", "[]", now.AddDays(-1), "{}", now.AddDays(-10), gracePeriodDays: 7));
        var notifications = new NotificationRepository();
        var scanner = new LmsLicenseExpiryReminderService(repository, new NotificationService(notifications));

        var duringGrace = scanner.Scan(now);
        var afterGrace = scanner.Scan(now.AddDays(8));

        Assert.Equal(1, duringGrace.GracePeriodNotifications);
        Assert.Equal(0, duringGrace.ExpiredNotifications);
        Assert.Equal(1, afterGrace.ExpiredNotifications);
        Assert.Contains(notifications.Items, x => x.Title == "许可证进入宽限期");
        Assert.Contains(notifications.Items, x => x.Title == "许可证已到期");
    }

    [Fact]
    public void OperationsSnapshot_UsesSameDerivedStatusAndWarningWindowAsLicenseViews()
    {
        var now = new DateTime(2026, 7, 18, 12, 0, 0);
        var repository = new LicenseRepository();
        var pending = new LmsLicenseRequest("LMS-REQ-PENDING", "admin", "Velrix", null, "[]", null, "{}", now);
        pending.Submit();
        var approved = new LmsLicenseRequest("LMS-REQ-APPROVED", "admin", "Velrix", null, "[]", null, "{}", now);
        approved.SetStatus(LmsLicenseRequestStatus.Approved);
        var cancelled = new LmsLicenseRequest("LMS-REQ-CANCELLED", "admin", "Velrix", null, "[]", null, "{}", now);
        cancelled.Cancel();
        repository.Add(pending); repository.Add(approved); repository.Add(cancelled);
        var active = new LmsLicenseAuthorization(approved.Id, "LIC-ACTIVE-OPS", "opaque-1", "Velrix", "[]", now.AddDays(45), "{}", now);
        var expiring = new LmsLicenseAuthorization(approved.Id, "LIC-EXPIRING-OPS", "opaque-2", "Velrix", "[]", now.AddDays(30), "{}", now);
        var expired = new LmsLicenseAuthorization(approved.Id, "LIC-EXPIRED-OPS", "opaque-3", "Velrix", "[]", now.AddSeconds(-1), "{}", now);
        var disabled = new LmsLicenseAuthorization(approved.Id, "LIC-DISABLED-OPS", "opaque-4", "Velrix", "[]", null, "{}", now);
        disabled.SetStatus(LmsLicenseStatus.Disabled);
        var revoked = new LmsLicenseAuthorization(approved.Id, "LIC-REVOKED-OPS", "opaque-5", "Velrix", "[]", null, "{}", now);
        revoked.SetStatus(LmsLicenseStatus.Revoked);
        repository.Add(active); repository.Add(expiring); repository.Add(expired); repository.Add(disabled); repository.Add(revoked);
        repository.Add(new LmsLicenseLifecycleEntry(active.Id, LmsLicenseLifecycleAction.Disabled, LmsLicenseStatus.Active, LmsLicenseStatus.Disabled, "admin", "临时停用", now.AddMinutes(1)));
        repository.Add(new LmsLicenseLifecycleEntry(disabled.Id, LmsLicenseLifecycleAction.Enabled, LmsLicenseStatus.Disabled, LmsLicenseStatus.Active, "operator", "恢复授权", now.AddMinutes(2)));

        var snapshot = new LmsLicenseOperationsSnapshotService(repository).Get(now);

        Assert.Equal(3, snapshot.RequestCount);
        Assert.Equal(1, snapshot.PendingApprovalCount);
        Assert.Equal(1, snapshot.ApprovedRequestCount);
        Assert.Equal(1, snapshot.CancelledRequestCount);
        Assert.Equal(2, snapshot.ActiveAuthorizationCount);
        Assert.Equal(1, snapshot.ExpiringAuthorizationCount);
        Assert.Equal(1, snapshot.ExpiredAuthorizationCount);
        Assert.Equal(1, snapshot.DisabledAuthorizationCount);
        Assert.Equal(1, snapshot.RevokedAuthorizationCount);
        Assert.Equal(2, snapshot.Activities.Count);
        Assert.Equal("LIC-DISABLED-OPS", snapshot.Activities[0].LicenseNo);
        Assert.Equal(LmsLicenseLifecycleAction.Enabled, snapshot.Activities[0].Action);
        Assert.Equal("LIC-ACTIVE-OPS", snapshot.Activities[1].LicenseNo);
    }

    [Fact]
    public void DeleteDraft_RemovesOnlyDraftAndProtectsSubmittedRequest()
    {
        var draft = new LmsLicenseRequest("LMS-REQ-DRAFT-DELETE", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var submitted = new LmsLicenseRequest("LMS-REQ-SUBMITTED-DELETE", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        submitted.Submit();
        var repository = new LicenseRepository(draft, submitted);
        var service = new LmsLicenseService(repository);

        service.DeleteDraft(draft, "admin");

        Assert.DoesNotContain(repository.ListRequests(), x => x.Id == draft.Id);
        var error = Assert.Throws<InvalidOperationException>(() => service.DeleteDraft(submitted, "admin"));
        Assert.Contains("只有草稿", error.Message);
        Assert.Contains(repository.ListRequests(), x => x.Id == submitted.Id);
    }

    [Fact]
    public void DeleteDraft_RejectsAnotherApplicantsDraft_UnlessAdministrator()
    {
        var draft = new LmsLicenseRequest("LMS-REQ-DRAFT-SCOPE", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var service = new LmsLicenseService(new LicenseRepository(draft));

        var error = Assert.Throws<InvalidOperationException>(() => service.DeleteDraft(draft, "bob"));
        Assert.Contains("申请人本人或管理员", error.Message);
        Assert.Contains(draft, service.ListRequests());

        service.DeleteDraft(draft, "admin", isAdministrator: true);
        Assert.DoesNotContain(draft, service.ListRequests());
    }

    [Fact]
    public void SubmitAndResubmit_RejectAnotherApplicantsRequestBeforeWorkflowAccess()
    {
        var request = new LmsLicenseRequest("LMS-REQ-SUBMIT-SCOPE", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var service = new LmsLicenseService(new LicenseRepository(request));

        var submitError = Assert.Throws<InvalidOperationException>(() => service.Submit(request, "bob"));
        Assert.Contains("申请人本人或管理员", submitError.Message);
        Assert.Equal(LmsLicenseRequestStatus.Draft, request.Status);

        var workflowError = Assert.Throws<InvalidOperationException>(() => service.SubmitAndStartWorkflow(request, "bob"));
        Assert.Contains("申请人本人或管理员", workflowError.Message);
        request.Submit();

        var resubmitError = Assert.Throws<InvalidOperationException>(() => service.ResubmitAfterWithdrawal(request, "bob"));
        Assert.Contains("申请人本人或管理员", resubmitError.Message);
        Assert.Equal(LmsLicenseRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void Cancel_AllowsDraftAndSubmitted_ButCancelledIsTerminal()
    {
        var draft = new LmsLicenseRequest("LMS-REQ-DRAFT-CANCEL", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var submitted = new LmsLicenseRequest("LMS-REQ-SUBMITTED-CANCEL", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        submitted.Submit();
        var service = new LmsLicenseService(new LicenseRepository(draft, submitted));

        service.Cancel(draft, "admin", "用户撤销草稿");
        service.Cancel(submitted, "admin", "用户终止审批");

        Assert.Equal(LmsLicenseRequestStatus.Cancelled, draft.Status);
        Assert.Equal(LmsLicenseRequestStatus.Cancelled, submitted.Status);
        Assert.Throws<InvalidOperationException>(() => draft.Submit());
        Assert.Throws<InvalidOperationException>(() => service.Cancel(draft, "admin"));
    }

    [Fact]
    public void Cancel_RejectsAnotherApplicantsRequest_UnlessAdministrator()
    {
        var request = new LmsLicenseRequest("LMS-REQ-CANCEL-SCOPE", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var service = new LmsLicenseService(new LicenseRepository(request));

        var error = Assert.Throws<InvalidOperationException>(() => service.Cancel(request, "bob", "越权取消"));
        Assert.Contains("申请人本人或管理员", error.Message);
        Assert.Equal(LmsLicenseRequestStatus.Draft, request.Status);

        service.Cancel(request, "admin", "管理员取消", isAdministrator: true);
        Assert.Equal(LmsLicenseRequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void Cancel_PublishesOneApplicantNotification_WithReason()
    {
        var request = new LmsLicenseRequest("LMS-REQ-CANCEL-NOTICE", "Applicant", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var notificationRepository = new NotificationRepository();
        var service = new LmsLicenseService(new LicenseRepository(request), notifications: new NotificationService(notificationRepository));

        service.Cancel(request, "admin", "不再需要授权", isAdministrator: true);

        var notification = Assert.Single(notificationRepository.Items);
        Assert.Equal("applicant", notification.Recipient);
        Assert.Equal("许可证申请已取消", notification.Title);
        Assert.Contains("不再需要授权", notification.Content);
        Assert.Equal($"/Lms/License?requestId={request.Id}", notification.Href);
        Assert.Equal($"lms-license-request:{request.Id}:cancelled", notification.DedupeKey);
    }

    [Fact]
    public void RequestDetail_AggregatesWorkflowInstancesAndHistoryWithoutDuplicatingBusinessData()
    {
        var request = new LmsLicenseRequest("LMS-REQ-DETAIL", "admin", "Velrix", null, "[\"report\"]", null, "{}", DateTime.Now);
        var instance = CreateInstance(request.Id);
        var operationRepository = new WorkflowOperationRepository(
            new WorkflowOperation(instance.Id, null, null, nameof(LmsLicenseRequest), request.Id, WorkflowOperationKind.Started, "admin", null, "发起", "detail-started", DateTime.Now.AddMinutes(-1)),
            new WorkflowOperation(instance.Id, null, null, nameof(LmsLicenseRequest), request.Id, WorkflowOperationKind.Approved, "reviewer", null, "通过", "detail-approved", DateTime.Now));
        var instanceRepository = new InstanceRepository();
        instanceRepository.Add(instance);
        var bindings = new WorkflowBindingService(
            new WorkflowDefinitionService(new DefinitionRepository(CreateApprovalDefinition())),
            new WorkflowInstanceService(instanceRepository));
        var detail = new LmsLicenseRequestDetailService(new LicenseRepository(request), bindings, operationRepository).Get(request.Id);

        Assert.NotNull(detail);
        Assert.Same(request, detail!.Request);
        Assert.Single(detail.Workflows);
        Assert.Equal(2, detail.History.Count);
        Assert.Equal(WorkflowOperationKind.Approved, detail.History[0].Kind);
        Assert.Equal("[\"report\"]", detail.Request.FeaturesJson);
    }

    [Fact]
    public void AccessScope_SeparatesApplicantRequestsAndAuthorizations_ButKeepsAdministratorFullAccess()
    {
        var alice = new LmsLicenseRequest("LMS-REQ-ALICE-SCOPE", "Alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var bob = new LmsLicenseRequest("LMS-REQ-BOB-SCOPE", "Bob", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var aliceAuthorization = new LmsLicenseAuthorization(alice.Id, "LIC-ALICE-SCOPE", "opaque-a", "Velrix", "[]", null, "{}", DateTime.Now);
        var bobAuthorization = new LmsLicenseAuthorization(bob.Id, "LIC-BOB-SCOPE", "opaque-b", "Velrix", "[]", null, "{}", DateTime.Now);
        var repository = new LicenseRepository(alice, bob);
        repository.Add(aliceAuthorization);
        repository.Add(bobAuthorization);
        var access = new LmsLicenseAccessService(repository);

        Assert.Single(access.ListRequests("alice", isAdministrator: false));
        Assert.Equal(alice.Id, access.ListRequests("alice", false)[0].Id);
        Assert.Single(access.ListAuthorizations("alice", false));
        Assert.Equal(aliceAuthorization.Id, access.ListAuthorizations("alice", false)[0].Id);
        Assert.Empty(access.ListRequests("unknown", false));
        Assert.Equal(2, access.ListRequests("unknown", true).Count);
        Assert.Equal(2, access.ListAuthorizations("unknown", true).Count);
        Assert.False(access.CanReadRequest(bob.Id, "alice", false));
    }

    [Fact]
    public void LicenseAttachments_EnforceSizeCountAndMimeBoundary()
    {
        var request = new LmsLicenseRequest("LMS-REQ-ATTACHMENT", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var licenses = new LicenseRepository(request);
        var foreignRequest = new LmsLicenseRequest("LMS-REQ-ATTACHMENT-FOREIGN", "alice", "Velrix", null, "[]", null, "{}", DateTime.Now);
        licenses.Add(foreignRequest);
        var attachmentRepository = new AttachmentRepository();
        var attachmentService = new AttachmentService(attachmentRepository, new AttachmentAuditRepository());
        var service = new LmsLicenseAttachmentService(licenses, attachmentService);

        Assert.Throws<UnauthorizedAccessException>(() => service.Register(foreignRequest.Id, "foreign.pdf", "application/pdf", [1], "bob"));
        Assert.Throws<UnauthorizedAccessException>(() => service.List(foreignRequest.Id, "bob"));
        service.Register(foreignRequest.Id, "foreign.pdf", "application/pdf", [1], "admin", isAdministrator: true);
        var sourceTagged = service.Register(request.Id, "license.pdf", "application/pdf", [1, 2, 3], "admin", otherInfo: "{\"source\":\"客户提供\"}");
        Assert.Equal("{\"source\":\"客户提供\"}", sourceTagged.OtherInfo);
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "license.exe", "application/octet-stream", [1], "admin"));
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "license.json", "text/plain", [1], "admin"));
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "payload.pdf", "application/pdf", [(byte)'M', (byte)'Z'], "admin"));
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "payload.json", "application/json", System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>"), "admin"));
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "large.pdf", "application/pdf", new byte[(2 * 1024 * 1024) + 1], "admin"));
        for (var index = 2; index <= 6; index++) service.Register(request.Id, $"license-{index}.pdf", "application/pdf", [1], "admin");
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "license-7.pdf", "application/pdf", [1], "admin"));
        request.SetStatus(LmsLicenseRequestStatus.Approved);
        Assert.Throws<InvalidOperationException>(() => service.Register(request.Id, "approved.pdf", "application/pdf", [1], "admin"));
    }

    [Fact]
    public void AuthorizationLifecycle_RestoresStatus_WhenAuditWriteFails()
    {
        var authorization = new LmsLicenseAuthorization(null, "LIC-LIFECYCLE-ROLLBACK", "opaque", "Velrix", "[]", null, "{}", DateTime.Now);
        var service = new LmsLicenseService(new FailingLifecycleRepository(authorization), transactions: new RollbackTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => service.DisableAuthorization(authorization, "admin", "模拟审计写入失败", isAdministrator: true));

        Assert.Equal(LmsLicenseStatus.Active, authorization.Status);
    }

    [Fact]
    public void ApprovedRequest_AllowsExternalLicenseRegistration_ButOtherStatesDoNot()
    {
        var repository = new LicenseRepository();
        var service = new LmsLicenseService(repository);
        var request = service.CreateRequest("LMS-REQ-002", "admin", "admin", "Velrix", null, "[\"报表\"]", null, "{}");
        request.Submit();
        Assert.Throws<InvalidOperationException>(() => service.RegisterExternalLicense(request.Id, "LIC-002", "opaque", "Velrix", "[]", null, "{}", "admin"));

        request.SetStatus(LmsLicenseRequestStatus.Approved);
        var authorization = service.RegisterExternalLicense(request.Id, "LIC-002", "opaque", "Velrix", "[\"报表\"]", null, "{\"source\":\"manual\"}", "admin");

        Assert.Equal(request.Id, authorization.RequestId);
        Assert.Single(repository.ListAuthorizations());
        Assert.Throws<InvalidOperationException>(() => service.RegisterExternalLicense(request.Id, "LIC-003", "opaque", "Other", "[]", null, "{}", "admin"));
    }

    [Fact]
    public void WorkflowAction_ChangesSubmittedRequest_AndRejectsInvalidTransition()
    {
        var request = new LmsLicenseRequest("LMS-REQ-003", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        request.Submit();
        var repository = new LicenseRepository(request);
        var notifications = new NotificationRepository();
        var handler = new LmsLicenseWorkflowActionHandler(repository, new NotificationService(notifications));
        var instance = CreateInstance(request.Id);
        var approved = new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(LmsLicenseRequest.Status), nameof(LmsLicenseRequestStatus.Approved));

        handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Approved, null), approved);

        Assert.Equal(LmsLicenseRequestStatus.Approved, request.Status);
        var notification = Assert.Single(notifications.Items);
        Assert.Equal("许可证申请已批准", notification.Title);
        Assert.Equal("admin", notification.Recipient);
        Assert.Equal($"/Lms/License?requestId={request.Id}", notification.Href);
        handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Approved, null), approved);
        Assert.Single(notifications.Items);
        var rejected = new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(LmsLicenseRequest.Status), nameof(LmsLicenseRequestStatus.Rejected));
        Assert.Throws<InvalidOperationException>(() => handler.Execute(new WorkflowActionContext(instance, WorkflowActionTrigger.Rejected, null), rejected));
    }

    [Fact]
    public void WorkflowAction_RejectedRequest_NotifiesApplicantWithComment()
    {
        var request = new LmsLicenseRequest("LMS-REQ-REJECTED-NOTICE", "Applicant", "Velrix", null, "[]", null, "{}", DateTime.Now);
        request.Submit();
        var notifications = new NotificationRepository();
        var handler = new LmsLicenseWorkflowActionHandler(new LicenseRepository(request), new NotificationService(notifications));
        var rejected = new WorkflowActionDefinition(WorkflowActionType.SetField, nameof(LmsLicenseRequest.Status), nameof(LmsLicenseRequestStatus.Rejected));

        Assert.Throws<InvalidOperationException>(() => handler.Execute(new WorkflowActionContext(CreateInstance(request.Id), WorkflowActionTrigger.Rejected, "   "), rejected));
        Assert.Equal(LmsLicenseRequestStatus.Submitted, request.Status);
        handler.Execute(new WorkflowActionContext(CreateInstance(request.Id), WorkflowActionTrigger.Rejected, "请补充机器信息"), rejected);

        Assert.Equal(LmsLicenseRequestStatus.Rejected, request.Status);
        var notification = Assert.Single(notifications.Items);
        Assert.Equal("许可证申请已驳回", notification.Title);
        Assert.Contains("请补充机器信息", notification.Content);
        Assert.Equal("applicant", notification.Recipient);
    }

    [Fact]
    public void SubmitAndStartWorkflow_RestoresRequestStatus_WhenWorkflowStartFails()
    {
        var request = new LmsLicenseRequest("LMS-REQ-004", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var repository = new LicenseRepository(request);
        var binding = new WorkflowBindingService(
            new WorkflowDefinitionService(new EmptyDefinitionRepository()),
            new WorkflowInstanceService(new EmptyInstanceRepository()));
        var service = new LmsLicenseService(repository, binding, new RollbackTransactionBoundary());

        Assert.Throws<InvalidOperationException>(() => service.SubmitAndStartWorkflow(request, "admin"));

        Assert.Equal(LmsLicenseRequestStatus.Draft, request.Status);
    }

    [Fact]
    public void SubmitAndStartWorkflow_PublishesApplicantNotificationAfterStart()
    {
        var request = new LmsLicenseRequest("LMS-REQ-SUBMIT-NOTICE", "Applicant", "Velrix", null, "[]", null, "{}", DateTime.Now);
        var definition = CreateApprovalDefinition();
        var instances = new InstanceRepository();
        var notifications = new NotificationRepository();
        var binding = new WorkflowBindingService(
            new WorkflowDefinitionService(new DefinitionRepository(definition)),
            new WorkflowInstanceService(instances));
        var service = new LmsLicenseService(new LicenseRepository(request), binding, notifications: new NotificationService(notifications));

        service.SubmitAndStartWorkflow(request, "admin", isAdministrator: true);

        Assert.Equal(LmsLicenseRequestStatus.Submitted, request.Status);
        var notification = Assert.Single(notifications.Items);
        Assert.Equal("许可证申请已提交审批", notification.Title);
        Assert.Equal("applicant", notification.Recipient);
        Assert.Equal($"/Lms/License?requestId={request.Id}", notification.Href);
        Assert.Single(instances.List(nameof(LmsLicenseRequest), request.Id, WorkflowInstanceStatus.Running));
    }

    [Fact]
    public void ResubmitAfterWithdrawal_ReopensRequest_AndLinksNewWorkflowInstance()
    {
        var request = new LmsLicenseRequest("LMS-REQ-005", "admin", "Velrix", null, "[]", null, "{}", DateTime.Now);
        request.Submit();
        var definition = CreateApprovalDefinition();
        var definitions = new DefinitionRepository(definition);
        var instances = new InstanceRepository();
        var instanceService = new WorkflowInstanceService(instances);
        var previous = instanceService.Start(definition, nameof(LmsLicenseRequest), request.Id, startedBy: "admin");
        instanceService.Cancel(previous);
        var service = new LmsLicenseService(new LicenseRepository(request), new WorkflowBindingService(new WorkflowDefinitionService(definitions), instanceService));

        service.ResubmitAfterWithdrawal(request, "admin");

        var current = Assert.Single(instances.List(nameof(LmsLicenseRequest), request.Id, WorkflowInstanceStatus.Running));
        Assert.Equal(previous.Id, current.PreviousInstanceId);
        Assert.Equal(LmsLicenseRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void ReplacementRequestAccess_RestrictsRegularUsersToOwnApplications()
    {
        var originalId = Guid.CreateVersion7();
        var own = new LmsLicenseReplacementRequest("LMS-REP-ACCESS-OWN", originalId, LmsLicenseReplacementKind.Renewal, null, "LIC-OWN", "opaque", null, "{}", "Alice", "续期", DateTime.Now);
        var other = new LmsLicenseReplacementRequest("LMS-REP-ACCESS-OTHER", Guid.CreateVersion7(), LmsLicenseReplacementKind.Reissue, null, "LIC-OTHER", "opaque", null, "{}", "Bob", "重发", DateTime.Now);
        var service = new LmsLicenseReplacementRequestService(new ReplacementRequestRepositoryWith(own, other), new LmsLicenseService(new LicenseRepository()));

        var visible = service.ListVisible("alice", isAdministrator: false);

        Assert.Equal([own], visible);
        Assert.True(service.CanRead(own.Id, "alice", isAdministrator: false));
        Assert.False(service.CanRead(other.Id, "alice", isAdministrator: false));
        Assert.Equal(2, service.ListVisible("auditor", isAdministrator: true).Count);
    }

    private static WorkflowInstance CreateInstance(Guid businessId)
    {
        var definition = new WorkflowDefinition(WorkflowBindingCodes.LmsLicenseApproval, "许可证申请审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        definition.Publish();
        return WorkflowInstance.Start(definition, nameof(LmsLicenseRequest), businessId, DateTime.Now);
    }

    private static WorkflowDefinition CreateApprovalDefinition(string definitionCode = WorkflowBindingCodes.LmsLicenseApproval)
    {
        var definition = new WorkflowDefinition(definitionCode, "许可证申请审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class LicenseRepository(params LmsLicenseRequest[] requests) : ILmsLicenseRepository
    {
        private readonly List<LmsLicenseRequest> requestItems = [.. requests];
        private readonly List<LmsLicenseAuthorization> authorizationItems = [];
        public IReadOnlyList<LmsLicenseRequest> ListRequests() => requestItems;
        public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations() => authorizationItems;
        public IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycleEntries(Guid authorizationId) => lifecycleItems.Where(x => x.AuthorizationId == authorizationId).ToArray();
        public void Add(LmsLicenseRequest item) => requestItems.Add(item);
        public void Update(LmsLicenseRequest item) { }
        public void RemoveRequest(Guid requestId) => requestItems.RemoveAll(x => x.Id == requestId);
        public void Add(LmsLicenseAuthorization item) => authorizationItems.Add(item);
        public void Update(LmsLicenseAuthorization item) { }
        public void Add(LmsLicenseLifecycleEntry item) => lifecycleItems.Add(item);
        private readonly List<LmsLicenseLifecycleEntry> lifecycleItems = [];
    }

    private sealed class ReplacementRequestRepository : ILmsLicenseReplacementRequestRepository
    {
        private readonly List<LmsLicenseReplacementRequest> items = [];
        public IReadOnlyList<LmsLicenseReplacementRequest> List() => items;
        public void Add(LmsLicenseReplacementRequest item) => items.Add(item);
        public void Update(LmsLicenseReplacementRequest item) { }
    }

    private sealed class ReplacementRequestRepositoryWith(params LmsLicenseReplacementRequest[] requests) : ILmsLicenseReplacementRequestRepository
    {
        private readonly List<LmsLicenseReplacementRequest> items = [.. requests];
        public IReadOnlyList<LmsLicenseReplacementRequest> List() => items;
        public void Add(LmsLicenseReplacementRequest item) => items.Add(item);
        public void Update(LmsLicenseReplacementRequest item) { }
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).Where(x => !unreadOnly || !x.IsRead).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification)
        {
            if (Items.Any(x => x.Recipient.Equals(notification.Recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == notification.DedupeKey)) return false;
            Items.Add(notification);
            return true;
        }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }

    private sealed class WorkflowOperationRepository(params WorkflowOperation[] operations) : IWorkflowOperationRepository
    {
        private readonly List<WorkflowOperation> items = [.. operations];
        public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null) => items.Where(x => instanceId is null || x.InstanceId == instanceId).Where(x => businessType is null || x.BusinessType.Equals(businessType, StringComparison.OrdinalIgnoreCase)).Where(x => businessId is null || x.BusinessId == businessId).Where(x => kind is null || x.Kind == kind).ToArray();
        public WorkflowOperation? FindByDedupeKey(string dedupeKey) => items.FirstOrDefault(x => x.DedupeKey == dedupeKey);
        public void Add(WorkflowOperation operation) => items.Add(operation);
        public bool TryAdd(WorkflowOperation operation)
        {
            if (items.Any(x => x.DedupeKey == operation.DedupeKey)) return false;
            items.Add(operation);
            return true;
        }
    }

    private sealed class AttachmentRepository : IAttachmentRepository
    {
        private readonly List<BusinessAttachment> items = [];
        public IReadOnlyList<BusinessAttachment> List(string? businessType = null, Guid? businessId = null, bool includeDeleted = false) => items.Where(x => businessType is null || x.BusinessType.Equals(businessType, StringComparison.OrdinalIgnoreCase)).Where(x => businessId is null || x.BusinessId == businessId).Where(x => includeDeleted || x.Status == BusinessAttachmentStatus.Active).ToArray();
        public void Add(BusinessAttachment item) => items.Add(item);
        public void Update(BusinessAttachment item) { }
    }

    private sealed class AttachmentAuditRepository : IAttachmentAuditRepository
    {
        private readonly List<AttachmentAuditEntry> items = [];
        public IReadOnlyList<AttachmentAuditEntry> List(Guid? attachmentId = null, Guid? businessId = null) => items.Where(x => attachmentId is null || x.AttachmentId == attachmentId).Where(x => businessId is null || x.BusinessId == businessId).ToArray();
        public void Add(AttachmentAuditEntry item) => items.Add(item);
    }

    private sealed class ProductRepository : ILmsLicenseProductRepository
    {
        private readonly List<LmsLicenseProduct> items = [];
        public IReadOnlyList<LmsLicenseProduct> List() => items;
        public void Add(LmsLicenseProduct item) => items.Add(item);
        public void Update(LmsLicenseProduct item) { }
    }

    private sealed class FeatureRepository : ILmsFeatureRepository
    {
        private readonly List<LmsFeature> items = [];
        public IReadOnlyList<LmsFeature> List() => items;
        public void Add(LmsFeature item) => items.Add(item);
        public void Update(LmsFeature item) { }
    }

    private sealed class FeatureVersionRepository : ILmsFeatureVersionRepository
    {
        private readonly List<LmsFeatureVersion> items = [];
        public IReadOnlyList<LmsFeatureVersion> List() => items;
        public void Add(LmsFeatureVersion item) => items.Add(item);
        public void Update(LmsFeatureVersion item) { }
    }

    private sealed class CustomerMachineRepository : ILmsCustomerMachineRepository
    {
        private readonly List<LmsCustomerMachine> items = [];
        public IReadOnlyList<LmsCustomerMachine> List() => items;
        public void Add(LmsCustomerMachine item) => items.Add(item);
        public void Update(LmsCustomerMachine item) { }
    }

    private sealed class CustomerFeatureRepository : ILmsCustomerFeatureRepository
    {
        private readonly List<LmsCustomerFeature> items = [];
        public IReadOnlyList<LmsCustomerFeature> List() => items;
        public void Add(LmsCustomerFeature item) => items.Add(item);
        public void Update(LmsCustomerFeature item) { }
    }

    private sealed class MachineFeatureRepository : ILmsMachineFeatureRepository
    {
        private readonly List<LmsMachineFeature> items = [];
        public IReadOnlyList<LmsMachineFeature> List() => items;
        public void Add(LmsMachineFeature item) => items.Add(item);
        public void Update(LmsMachineFeature item) { }
    }

    private sealed class CustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> items = [];
        public IReadOnlyList<Customer> List() => items;
        public void Add(Customer customer) => items.Add(customer);
        public void Update(Customer customer) { }
        public void Remove(Guid customerId) => items.RemoveAll(x => x.Id == customerId);
    }

    private sealed class ContactRepository : ICustomerContactRepository
    {
        private readonly List<CustomerContact> items = [];
        public IReadOnlyList<CustomerContact> List() => items;
        public void Add(CustomerContact contact) => items.Add(contact);
        public void Update(CustomerContact contact) { }
        public void ClearPrimary(Guid customerId, Guid exceptId) { }
        public void Remove(Guid contactId) => items.RemoveAll(x => x.Id == contactId);
    }

    private sealed class EmptyDefinitionRepository : IWorkflowDefinitionRepository
    {
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => [];
        public void Add(WorkflowDefinition definition) { }
        public bool TryAdd(WorkflowDefinition definition) => true;
        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) { }
    }

    private sealed class EmptyInstanceRepository : IWorkflowInstanceRepository
    {
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => [];
        public void Add(WorkflowInstance instance) { }
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class DefinitionRepository(params WorkflowDefinition[] definitions) : IWorkflowDefinitionRepository
    {
        private readonly List<WorkflowDefinition> items = [.. definitions];
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null) => items.Where(x => code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowDefinition definition) => items.Add(definition);
        public bool TryAdd(WorkflowDefinition definition)
        {
            if (items.Any(x => x.Id == definition.Id || (x.Code.Equals(definition.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == definition.VersionNumber))) return false;
            Add(definition);
            return true;
        }
        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => items.Where(x => businessType is null || x.BusinessType == businessType).Where(x => businessId is null || x.BusinessId == businessId).Where(x => status is null || x.Status == status).ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { if (items.Any(x => x.Id == instance.Id)) return false; Add(instance); return true; }
        public void Update(WorkflowInstance instance) { }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class LmsServiceProvider(LmsLicenseService licenses) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(LmsLicenseService) ? licenses : null;
    }

    private sealed class RollbackTransactionBoundary : IWorkflowTransactionBoundary
    {
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            try { operation(); }
            catch (Exception exception) { afterRollback?.Invoke(exception); throw; }
        }
    }

    private sealed class FailingLifecycleRepository(LmsLicenseAuthorization authorization) : ILmsLicenseRepository
    {
        public IReadOnlyList<LmsLicenseRequest> ListRequests() => [];
        public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations() => [authorization];
        public IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycleEntries(Guid authorizationId) => [];
        public void Add(LmsLicenseRequest item) { }
        public void Update(LmsLicenseRequest item) { }
        public void RemoveRequest(Guid requestId) { }
        public void Add(LmsLicenseAuthorization item) { }
        public void Update(LmsLicenseAuthorization item) { }
        public void Add(LmsLicenseLifecycleEntry item) => throw new InvalidOperationException("模拟审计写入失败");
    }
}
