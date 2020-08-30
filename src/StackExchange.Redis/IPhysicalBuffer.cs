namespace StackExchange.Redis
{
    /// <summary>
    /// Abstraction over a physical buffer
    /// for serializing Redis commands.
    /// </summary>
    internal interface IPhysicalBuffer
    {
        PhysicalConnection Connection { get; }

        PhysicalBridge BridgeCouldBeNull { get; }

        void WriteHeader(RedisCommand command, int arguments, CommandBytes commandBytes = default);

        void WriteBulkString(RedisValue value);

        void Write(RedisKey key);

        void WriteSha1AsHex(byte[] hexHash);

        void Write(RedisChannel channel);
    }
}