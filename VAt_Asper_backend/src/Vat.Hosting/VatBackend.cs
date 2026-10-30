using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vat.Api;
using Vat.Application;
using Vat.Infrastructure.Persistence;
using Vat.Infrastructure.Messaging;
using Vat.Web;
using Vat.Messaging.Contracts.V1;
using Vat.Messaging;

namespace Vat.Hosting;

// Composition bridge for contexts whose financial ownership has not yet been extracted.
public static class VatBackend
{
    public static WebApplicationBuilder AddVatBackend(this WebApplicationBuilder builder, string serviceName)
    {
        builder.AddVatHttp(serviceName);
        builder.Services.AddOptions<DatabaseSettings>().BindConfiguration("ConnectionStrings")
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.VatDatabase), "Configure ConnectionStrings:VatDatabase.")
            .ValidateOnStart();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDbContext<VatDbContext>((services, options) =>
            options.UseNpgsql(services.GetRequiredService<IOptions<DatabaseSettings>>().Value.VatDatabase));
        builder.Services.AddScoped<ICompanyStore, CompanyStore>();
        builder.Services.AddScoped<IPurchaseStore, PurchaseStore>();
        builder.Services.AddScoped<RecordWriters>();
        builder.Services.AddScoped<IEventWriter>(sp => sp.GetRequiredService<RecordWriters>());
        builder.Services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<RecordWriters>());
        builder.Services.AddScoped<ICommandExecutor, CommandExecutor>();
        builder.Services.AddScoped<CompanyService>();
        builder.Services.AddScoped<PeriodService>();
        builder.Services.AddScoped<PurchaseService>();
        builder.Services.AddScoped<SalesInvoiceService>();
        builder.Services.AddScoped<CustomerService>();
        builder.Services.AddScoped<CustomerReportingService>();
        builder.Services.AddScoped<ICustomerReportingStore, CustomerReportingStore>();
        builder.Services.AddScoped<PaymentService>();
        builder.Services.AddScoped<PaymentCorrectionService>();
        builder.Services.AddScoped<IPaymentCorrectionStore, PaymentCorrectionStore>();
        builder.Services.AddScoped<IPaymentStore, PaymentStore>();
        builder.Services.AddScoped<ICustomerStore, CustomerStore>();
        builder.Services.AddScoped<SalesPostingService>();
        builder.Services.AddScoped<SalesCreditService>();
        builder.Services.AddScoped<SalesDebitService>();
        builder.Services.AddScoped<ISalesDebitStore, SalesDebitStore>();
        builder.Services.AddScoped<ISalesCreditStore, SalesCreditStore>();
        builder.Services.AddScoped<ISalesPostingStore, SalesPostingStore>();
        builder.Services.AddScoped<ISalesInvoiceStore, SalesInvoiceStore>();
        builder.Services.AddOptions<PostingSettings>().BindConfiguration("Posting");
        builder.Services.AddScoped(services => new PostingPolicy(services.GetRequiredService<IOptions<PostingSettings>>().Value.Enabled));
        builder.Services.AddScoped<IReferenceStore, ReferenceStore>();
        builder.Services.AddScoped<IPostingStore, PostingStore>();
        builder.Services.AddScoped<ReferenceDataService>();
        builder.Services.AddScoped<IInventoryReadStore, InventoryReadStore>();
        builder.Services.AddScoped<InventoryReadService>();
        builder.Services.AddScoped<FinancialInventoryQueries>();
        builder.Services.AddScoped<ITransferStore, TransferStore>();
        builder.Services.AddScoped<TransferService>();
        builder.Services.AddScoped<PostingService>();
        builder.Services.AddScoped<IPeriodStore, PeriodStore>();
        builder.Services.AddScoped<IPeriodGuard, PeriodGuard>();
        builder.Services.AddScoped<IReversalStore, ReversalStore>();
        builder.Services.AddScoped<ReversalService>();
        builder.Services.AddOptions<MessagingSettings>().BindConfiguration("Messaging")
            .Validate(settings => settings.IsValid(), "Messaging settings are invalid; require valid names, bounded retry timings and a TLS broker URI (loopback may use AMQP).")
            .ValidateOnStart();
        builder.Services.AddSingleton<RabbitMqTransport>();
        builder.Services.AddSingleton<IEventPublisher>(services => services.GetRequiredService<RabbitMqTransport>());
        builder.Services.AddScoped<IOutboxLeaseSourceV1, FinancialOutboxLeaseSourceV1>();
        builder.Services.AddScoped<OutboxDispatcher>();
        builder.Services.AddScoped<ReportingInbox>();
        builder.Services.AddScoped<IIncomingEventProcessor>(services => services.GetRequiredService<ReportingInbox>());
        builder.Services.AddScoped<IInboxDeliveryTargetV1>(services => services.GetRequiredService<ReportingInbox>());
        builder.Services.AddScoped<InboxDeliveryHandler>();
        builder.Services.AddScoped<IReportingService, ReportingService>();
        builder.Services.AddScoped<ReportingSourceExportService>();
        builder.Services.AddScoped<ReportingSourceJournal>();
        builder.Services.AddScoped<CustomerHistoryExportService>();
        if (serviceName is "Integration" or "Compatibility") builder.Services.AddHostedService<MessagingWorker>();
        builder.Services.AddScoped<Vat.Services.MasterData.MasterDataQueries>();
        builder.Services.AddScoped<IBackendReadiness, FinancialReadiness>();
        return builder;
    }

    public static WebApplication MapVatBackend(this WebApplication app) => app.MapVatHttp();
}

public sealed class FinancialReadiness(VatDbContext db) : IBackendReadiness
{
    public async Task<bool> IsReady(CancellationToken ct) => !(await db.Database.GetPendingMigrationsAsync(ct)).Any();
}
