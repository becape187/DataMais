using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DataMais.Configuration;
using DataMais.Services;

namespace DataMais.Data;

public class DataMaisDbContextFactory : IDesignTimeDbContextFactory<DataMaisDbContext>
{
    public DataMaisDbContext CreateDbContext(string[] args)
    {
        // Configuração hardcoded para modec.automais.cloud
        var connectionString = "Host=modec.automais.cloud;Port=5432;Database=datamais;Username=postgres;Password=#030957#Be8654";

        var optionsBuilder = new DbContextOptionsBuilder<DataMaisDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DataMaisDbContext(optionsBuilder.Options);
    }
}
