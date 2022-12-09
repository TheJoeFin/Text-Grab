
using System;
using System.Threading;

namespace Text_Grab;

public class NullAsyncResult : IAsyncResult
{
    public object? AsyncState => null;

    public WaitHandle AsyncWaitHandle => new NullWaitHandle();

    public bool CompletedSynchronously => true;

    public bool IsCompleted => true;
}

public class NullWaitHandle : WaitHandle
{

}