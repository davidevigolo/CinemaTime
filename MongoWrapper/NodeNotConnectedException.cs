using System;

namespace MongoWrapper.MongoCore
{
    public class NodeNotConnectedException : Exception
    {
        public NodeNotConnectedException() : base("Node not connected yet.") { }
    }
}
