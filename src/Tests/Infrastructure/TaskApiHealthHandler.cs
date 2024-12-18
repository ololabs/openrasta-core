using OpenRasta.Web;

namespace Tests.Infrastructure
{
  public class TaskApiHealthHandler
  {
    public void GetSilent()
    {
    }

    public OperationResult.OK GetNoContent() => new OperationResult.OK();

    public OperationResult.OK GetException() => throw new System.Exception("This is a test exception");
  }
}