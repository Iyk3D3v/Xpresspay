namespace XpressPay.Infrastructure.BackgroundJobs;

public class TransientEmailGatewayException : Exception
{
    public TransientEmailGatewayException(string message) : base(message)
    {
    }

    public TransientEmailGatewayException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
