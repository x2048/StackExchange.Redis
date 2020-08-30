using System;
using System.IO.Pipelines;

namespace StackExchange.Redis
{
    internal sealed partial class PhysicalConnection
    {
        internal sealed class Formatter : IPhysicalBuffer
        {
            PipeWriter _writer;
            PhysicalConnection _connection;

            public Formatter(PipeWriter writer, PhysicalConnection connection)
            {
                _writer = writer;
                _connection = connection;
            }

            public PhysicalConnection Connection => _connection;

            public PhysicalBridge BridgeCouldBeNull => _connection?.BridgeCouldBeNull;

            public void Write(in RedisKey key)
            {
                var val = key.KeyValue;
                if (val is string s)
                {
                    WriteUnifiedPrefixedString(_writer, key.KeyPrefix, s);
                }
                else
                {
                    WriteUnifiedPrefixedBlob(_writer, key.KeyPrefix, (byte[])val);
                }                
            }

            public void Write(in RedisChannel channel) => WriteUnifiedPrefixedBlob(_writer, _connection.ChannelPrefix, channel.Value);

            public void WriteBulkString(RedisValue value) => PhysicalConnection.WriteBulkString(value, _writer);

            void IPhysicalBuffer.WriteHeader(RedisCommand command, int arguments, CommandBytes commandBytes)
            {
                var bridge = BridgeCouldBeNull;
                if (bridge == null) throw new ObjectDisposedException(ToString());

                if (command == RedisCommand.UNKNOWN)
                {
                    // using >= here because we will be adding 1 for the command itself (which is an arg for the purposes of the multi-bulk protocol)
                    if (arguments >= REDIS_MAX_ARGS) throw ExceptionFactory.TooManyArgs(commandBytes.ToString(), arguments);
                }
                else
                {
                    // using >= here because we will be adding 1 for the command itself (which is an arg for the purposes of the multi-bulk protocol)
                    if (arguments >= REDIS_MAX_ARGS) throw ExceptionFactory.TooManyArgs(command.ToString(), arguments);

                    // for everything that isn't custom commands: ask the muxer for the actual bytes
                    commandBytes = bridge.Multiplexer.CommandMap.GetBytes(command);
                }

                // in theory we should never see this; CheckMessage dealt with "regular" messages, and
                // ExecuteMessage should have dealt with everything else
                if (commandBytes.IsEmpty) throw ExceptionFactory.CommandDisabled(command);

                // *{argCount}\r\n      = 3 + MaxInt32TextLen
                // ${cmd-len}\r\n       = 3 + MaxInt32TextLen
                // {cmd}\r\n            = 2 + commandBytes.Length
                var span = _writer.GetSpan(commandBytes.Length + 8 + MaxInt32TextLen + MaxInt32TextLen);
                span[0] = (byte)'*';

                int offset = WriteRaw(span, arguments + 1, offset: 1);

                offset = AppendToSpanCommand(span, commandBytes, offset: offset);

                _writer.Advance(offset);
            }

            public void WriteSha1AsHex(byte[] hexHash) => PhysicalConnection.WriteSha1AsHex(hexHash, _writer);
        }
    }
}
