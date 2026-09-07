namespace LockStep.Server;

public abstract class ManagerBase
{
    protected void Register()
    {
        GameEntry.Register(this);
    }
}
