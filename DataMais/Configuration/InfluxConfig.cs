namespace DataMais.Configuration;

public class InfluxConfig
{
    public string Url { get; set; } = "http://localhost:8086";
    public string Token { get; set; } = string.Empty;
    public string Organization { get; set; } = "datamais";
    public string Bucket { get; set; } = "sensores";
}
