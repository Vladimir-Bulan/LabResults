using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LabResults.Domain.Aggregates;
using LabResults.Domain.Enums;
using LabResults.Domain.Ports;
using LabResults.Domain.ValueObjects;

namespace LabResults.Infrastructure.Persistence
{
    public class LabResultsDbContext : DbContext
    {
        public LabResultsDbContext(DbContextOptions<LabResultsDbContext> options) : base(options) { }
        public DbSet<Sample> Samples => Set<Sample>();
        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Ignore<LabResults.Domain.Common.DomainEvent>();
            mb.Entity<Sample>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Id).ValueGeneratedNever();
                b.OwnsOne(x => x.Code, o => o.Property(c => c.Value).HasColumnName("Code").IsRequired());
                b.OwnsOne(x => x.PatientId, o => o.Property(c => c.Value).HasColumnName("PatientIdValue").IsRequired());
                b.OwnsOne(x => x.ValidatedBy, o => o.Property(c => c.Value).HasColumnName("ValidatedById"));
                b.Property(x => x.AnalysisType).HasConversion<string>();
                b.Property(x => x.Status).HasConversion<string>();
                b.Property(x => x.ResultStatus).HasConversion<string>();
                b.OwnsOne(x => x.Result, r =>
                {
                    r.Property(x => x.Id).HasColumnName("ResultId");
                    r.Property(x => x.Notes).HasColumnName("ResultNotes");
                    r.Property(x => x.CompletedAt).HasColumnName("ResultCompletedAt");
                    r.Property(x => x.Type).HasConversion<string>().HasColumnName("ResultType");
                    r.OwnsOne(x => x.Value, v =>
                    {
                        v.Property(x => x.Numeric).HasColumnName("ResultNumeric");
                        v.Property(x => x.Unit).HasColumnName("ResultUnit");
                        v.Property(x => x.ReferenceMin).HasColumnName("ResultRefMin");
                        v.Property(x => x.ReferenceMax).HasColumnName("ResultRefMax");
                    });
                });
                
                b.HasIndex(x => x.Status);
                b.ToTable("Samples");
            });
        }
    }
}

namespace LabResults.Infrastructure.Adapters
{
    using LabResults.Infrastructure.Persistence;
    public class SampleRepository : ISampleRepository
    {
        private readonly LabResultsDbContext _db;
        public SampleRepository(LabResultsDbContext db) => _db = db;
        public async Task<Sample?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Samples.FirstOrDefaultAsync(s => s.Id == id, ct);
        public async Task<Sample?> GetByCodeAsync(string code, CancellationToken ct = default)
            => await _db.Samples.FromSqlRaw("SELECT * FROM \"Samples\" WHERE \"Code\" = {0}", code.ToUpperInvariant()).FirstOrDefaultAsync(ct);
        public async Task<IEnumerable<Sample>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default)
            => await _db.Samples.Where(s => EF.Property<Guid>(s, "PatientIdValue") == patientId).ToListAsync(ct);
        public async Task<IEnumerable<Sample>> GetPendingValidationAsync(CancellationToken ct = default)
            => await _db.Samples.Where(s => s.Status == SampleStatus.Completed).ToListAsync(ct);
        public async Task AddAsync(Sample sample, CancellationToken ct = default)
        {
            await _db.Samples.AddAsync(sample, ct);
            await _db.SaveChangesAsync(ct);
        }
        public async Task UpdateAsync(Sample sample, CancellationToken ct = default)
        {
            _db.Samples.Update(sample);
            await _db.SaveChangesAsync(ct);
        }
    }
    public class ConsoleEmailAdapter : INotificationPort
    {
        private readonly ILogger<ConsoleEmailAdapter> _logger;
        public ConsoleEmailAdapter(ILogger<ConsoleEmailAdapter> logger) => _logger = logger;
        public Task SendResultReadyEmailAsync(string patientEmail, string patientName, string sampleCode, CancellationToken ct = default)
        {
            _logger.LogInformation("[EMAIL] Ready for {N} ({E}) - {C}", patientName, patientEmail, sampleCode);
            return Task.CompletedTask;
        }
        public Task SendAbnormalResultAlertAsync(string doctorEmail, string sampleCode, string analysisType, CancellationToken ct = default)
        {
            _logger.LogWarning("[EMAIL] ABNORMAL {E} - {C} ({T})", doctorEmail, sampleCode, analysisType);
            return Task.CompletedTask;
        }
    }
    public class PdfAdapter : IPdfPort
    {
        private readonly LabResultsDbContext _db;
        private readonly ILogger<PdfAdapter> _logger;
        public PdfAdapter(LabResultsDbContext db, ILogger<PdfAdapter> logger) { _db = db; _logger = logger; }
        public async Task<byte[]> GenerateResultPdfAsync(Guid sampleId, CancellationToken ct = default)
        {
            var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == sampleId, ct);
            if (sample == null) return Array.Empty<byte>();
            var sb = new StringBuilder();
            sb.AppendLine("LAB RESULT REPORT");
            sb.AppendLine("Sample: " + sample.Code.Value);
            sb.AppendLine("Patient: " + sample.PatientId.Value);
            sb.AppendLine("Analysis: " + sample.AnalysisType);
            sb.AppendLine("Status: " + sample.Status);
            sb.AppendLine("Date: " + sample.ReceivedAt.ToString("yyyy-MM-dd"));
            if (sample.Result != null)
            {
                sb.AppendLine("Result: " + sample.Result.Value.Numeric + " " + sample.Result.Value.Unit + " (" + sample.Result.Value.Status + ")");
                sb.AppendLine("Notes: " + sample.Result.Notes);
            }
            _logger.LogInformation("[PDF] Generated for {Code}", sample.Code.Value);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
    public class RedisCacheAdapter : ICachePort
    {
        private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;
        public RedisCacheAdapter(Microsoft.Extensions.Caching.Distributed.IDistributedCache cache) => _cache = cache;
        public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            var bytes = await _cache.GetAsync(key, ct);
            if (bytes == null) return default;
            return System.Text.Json.JsonSerializer.Deserialize<T>(bytes);
        }
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            var opts = new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
            };
            await _cache.SetAsync(key, bytes, opts, ct);
        }
        public async Task RemoveAsync(string key, CancellationToken ct = default)
            => await _cache.RemoveAsync(key, ct);
    }
}

namespace LabResults.Infrastructure
{
    using LabResults.Infrastructure.Adapters;
    using LabResults.Infrastructure.Persistence;
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<LabResultsDbContext>(opts => opts.UseNpgsql(config.GetConnectionString("LabResults")));
            services.AddStackExchangeRedisCache(opts => opts.Configuration = config.GetConnectionString("Redis"));
            services.AddScoped<ISampleRepository, SampleRepository>();
            services.AddScoped<INotificationPort, ConsoleEmailAdapter>();
            services.AddScoped<IPdfPort, PdfAdapter>();
            services.AddScoped<ICachePort, RedisCacheAdapter>();
            return services;
        }
    }
}
namespace LabResults.Infrastructure.Persistence {
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Design;
    public class LabResultsDbContextFactory : IDesignTimeDbContextFactory<LabResultsDbContext> {
        public LabResultsDbContext CreateDbContext(string[] args) {
            var opts = new DbContextOptionsBuilder<LabResultsDbContext>();
            opts.UseNpgsql("Host=localhost;Port=5435;Database=labresults;Username=walletflow;Password=walletflow123");
            return new LabResultsDbContext(opts.Options);
        }
    }
}

