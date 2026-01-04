namespace AzureDnsExternalIpSync.Cli.Options
{
    public class AwsLambdaOptions
    {
        public const string SectionName = "AwsLambda";

        public string FunctionUrl { get; set; }
        public string AccessKeyId { get; set; }
        public string SecretAccessKey { get; set; }
        public string Region { get; set; } = "eu-central-1";
    }
}
