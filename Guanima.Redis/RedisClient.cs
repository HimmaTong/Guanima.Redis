﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Guanima.Redis.Commands.Connection;
using Guanima.Redis.Commands.Generic;
using Guanima.Redis.Configuration;
using Guanima.Redis.Commands;
using Guanima.Redis.Protocol;
using Guanima.Redis.Utils;

namespace Guanima.Redis
{
    public partial class RedisClient : Disposable
    {
        /// Represents a value which indicates that an item should never expire.
		/// </summary>
		public static readonly TimeSpan Infinite = TimeSpan.Zero;

		internal static RedisClientSection DefaultSettings = ConfigurationManager.GetSection("Guanima/Redis") as RedisClientSection;

        private static log4net.ILog log = log4net.LogManager.GetLogger(typeof(RedisClient));

        private IServerPool _serverPool;
        private Stack<IRedisNode> _onNodeStack;
        private RedisProtocol protocolHandler = new RedisProtocol();

        #region Constructor

        /// <summary>
		/// Initializes a new RedisClient instance using the default configuration section (Guanima/Redis).
		/// </summary>
		public RedisClient()
		{
			Initialize(DefaultSettings);
		}

		/// <summary>
		/// Initializes a new RedisClient instance using the specified configuration section. 
		/// This overload allows to create multiple RedisClients with different pool configurations.
		/// </summary>
		/// <param name="sectionName">The name of the configuration section to be used for configuring the behavior of the client.</param>
		public RedisClient(string sectionName)
		{
			var section = (RedisClientSection)ConfigurationManager.GetSection(sectionName);
			if (section == null)
				throw new ConfigurationErrorsException("Section " + sectionName + " is not found.");

			Initialize(section);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:Guanima.Redis.RedisClient"/> using the specified configuration instance.
		/// </summary>
		/// <param name="configuration">The client configuration.</param>
        public RedisClient(IRedisClientConfiguration configuration)
		{
			if (configuration == null)
				throw new ArgumentNullException("configuration");

			Initialize(configuration);
		}

        private void Initialize(IRedisClientConfiguration configuration)
        {
            IServerPool pool = new DefaultServerPool(configuration);

            Initialize(pool);
            _serverPool = pool;
            _currentDb = configuration.DefaultDB; // ???
        }

        private static void Initialize(IServerPool pool)
        {
            // everything is initialized, start the pool
            pool.Start();
        }
        
        #endregion

        #region Commands

        #endregion

        #region Nodes/Servers

        public IDisposable On(string alias)
        {
            var node = GetNodeByAlias(alias);
            if (node == null)
                throw new RedisClientException("No node found with the alias : '" + alias + "'");
            return On(node);
        }

        public IDisposable On(IRedisNode node)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            if (!node.IsAlive)
                throw new RedisClientException("The node is not responding : " + node.Alias);
            if (_onNodeStack == null)
                _onNodeStack = new Stack<IRedisNode>();

            _onNodeStack.Push(node);
            return new DisposableAction(() =>
                                            {
                                                _onNodeStack.Pop();
                                                if (_onNodeStack.Count == 0)
                                                    _onNodeStack = null;
                                            });
        }

        public String TransformKey(string key)
        {
            var kt = _serverPool.KeyTransformer;
            return (kt == null) ? key : kt.Transform(key);
        }

        public IEnumerable<string> TransformKeys(IEnumerable<String> keys)
        {
            var kt = _serverPool.KeyTransformer;
            if (kt == null)
                return keys;
            var transformed = new List<string>();
            foreach (var original in keys)
            {
                transformed.Add( kt.Transform(original) );
            }
            return transformed;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">Cache item key</param>
        /// <returns></returns>
        protected IRedisNode GetNodeForTransformedKey(string key)
        {
            if (_onNodeStack != null && _onNodeStack.Count > 0)
            {
                return _onNodeStack.Peek();
            }
            var locator = _serverPool.NodeLocator;
            return locator.Locate(key);
        }

        public IRedisNode GetNodeForKey(string key)
        {
            return GetNodeForTransformedKey(TransformKey(key));
        }

        public IRedisNode GetNodeByAlias(string alias)
        {
            if (String.IsNullOrEmpty(alias))
                throw new ArgumentNullException(alias);
            return _serverPool.GetServers().Where(x => x.Alias == alias).FirstOrDefault();
        }

        /// <summary>
        /// Maps each key in the list to a RedisNode
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        protected Dictionary<IRedisNode, List<string>> SplitKeys(IEnumerable<string> keys)
        {
            var retval = new Dictionary<IRedisNode, List<string>>(RedisNode.Comparer.Instance);

            foreach (var key in keys)
            {
                var node = GetNodeForTransformedKey(key);
                if (node != null)
                {
                    List<string> list;

                    if (!retval.TryGetValue(node, out list))
                        retval[node] = list = new List<string>();

                    list.Add(key);
                }
            }

            return retval;
        }

        private int _workingServerCount = -1;

        protected bool Clustering
        {
            get
            {
                if (_workingServerCount == -1)
                {
                    // this assumes the list doesnt change at runtime
                    _workingServerCount = _serverPool.GetServers().Count();
                }
                return (_workingServerCount > 1) &&
                       (_onNodeStack == null || _onNodeStack.Count == 0);
            }
        }

        protected void ForeachServer(Action<IRedisNode> action)
        {
            if (_onNodeStack != null && _onNodeStack.Count > 0)
            {
                action(_onNodeStack.Peek());
                return;
            }

            foreach (var server in _serverPool.GetServers())
            {
                if (!server.IsAlive)
                    continue;
                action(server);
            }
        }

        protected void ForeachServer(RedisCommand command)
        {
            if (_onNodeStack != null && _onNodeStack.Count > 0)
            {
                Execute(_onNodeStack.Peek(), command);
                return;
            }

            ForeachServer(node => Execute(node, command));
        }

        #endregion

        #region Sockets

        protected void DisposeSocket(PooledSocket socket)
        {
            if (socket != null)
                ((IDisposable)socket).Dispose();
        }


        protected PooledSocket AcquireSocket(IRedisNode node)
        {
            if (CheckDisposed(true))
                return null;
            var socket = node.Acquire();
            if (socket == null)
                throw new RedisClientException("Unable to acquire socket for node : '" + node.EndPoint + "'");

            try
            {
                if (!String.IsNullOrEmpty(node.Password) && !socket.IsAuthorized)
                {
                    protocolHandler.Socket = socket;
                    var command = new AuthCommand(node.Password);
                    string status = command.Execute(protocolHandler);
                    if (status != "OK")
                    {
                        throw new RedisAuthenticationException("Invalid credentials for node : " + node.Alias);
                    }
                    socket.IsAuthorized = true;
                }
                // Select proper db if specified in config or (socket.CurrentDB <> currentDB)
                // Read the comments on Select to get some background on the following.
                // Im not sure i like this.
                if (socket.CurrentDb != CurrentDB)
                {
                    protocolHandler.Socket = socket;
                    var command = new SelectCommand(CurrentDB);
                    command.Execute(protocolHandler);
                    socket.CurrentDb = CurrentDB;
                }
            } 
            catch
            {
                DisposeSocket(socket);
                throw;                
            }
            
            return socket;
        }

        #endregion

        #region Command Execution

        protected void CannotCluster(string commandName)
        {
            string message = String.Format("{0} cannot executed because the keys involved need to be on the same server or because we cannot guarantee that the operation will be atomic.", commandName);
            throw new RedisClusterException(message);
        }

        private void EnsureNotClustered(string commandName, IEnumerable<String> transformedKeys)
        {
            var splitKeys = SplitKeys(transformedKeys);
            if (splitKeys.Count > 1)
                CannotCluster(commandName);
        }

        private void EnsureNotClustered(string commandName, string transformedDestKey, IEnumerable<string> transformedKeys)
        {
            // See if all keys reside on the same node
            var splitKeys = SplitKeys(transformedKeys);
            var node = GetNodeForTransformedKey(transformedDestKey);
            bool clusteredKeys = false;
            if (splitKeys.Count == 1)
            {
                var firstNode = splitKeys.First().Key;
                if (firstNode != node)
                {
                    clusteredKeys = true;
                }
            }
            else
            {
                clusteredKeys = true;
            }
            if (clusteredKeys)
                CannotCluster(commandName);
        }

        // 
        private bool CheckEnqueue(IRedisNode node, RedisCommand command)
        {
            if (Pipelining || InTransaction)
            {
                EnqueueCommand(node, command);
                return true;
            }
            return false;
        }


        protected void HandleException(Exception ex)
        {
            // TODO generic catch-all does not seem to be a good idea now. Some errors (like command not supported by server) should be exposed 
            // while retaining the fire-and-forget behavior
            log.Error(ex);
        }


        protected void Execute(String key, RedisCommand command)
        {
            Execute(GetNodeForTransformedKey(key), command);
        }


        public RedisClient Execute(IRedisNode node, RedisCommand command)
        {
            Execute(node, command, true);
            return this;
        }

        internal void Execute(IRedisNode node, RedisCommand command, bool possiblyQueued)
        {
            if (possiblyQueued && CheckEnqueue(node, command))
                return;
            var socket = AcquireSocket(node);
            try
            {
                protocolHandler.Socket = socket;
                command.Execute(protocolHandler);
            }
            catch (Exception e)
            {
                // TODO generic catch-all does not seem to be a good idea now. Some errors (like command not supported by server) should be exposed 
                // while retaining the fire-and-forget behavior
                HandleException(e);
                throw;
            }
            finally
            {
                DisposeSocket(socket);
            }
        }


        protected RedisValue ExecValue(String key, RedisCommand command)
        {
            var node = GetNodeForTransformedKey(key);
            return ExecValue(node, command);
        }

        public RedisValue ExecValue(IRedisNode node, RedisCommand command)
        {
            return ExecValue(node, command, true);
        }

        private RedisValue ExecValue(IRedisNode node, RedisCommand command, bool possiblyQueued)
        {
            if (possiblyQueued && CheckEnqueue(node, command))
                return RedisValue.Empty;
            var socket = AcquireSocket(node);
            try
            {
                protocolHandler.Socket = socket;
                command.Execute(protocolHandler);
                return command.Result;
            }
            catch (Exception e)
            {
                // TODO generic catch-all does not seem to be a good idea now. Some errors (like command not supported by server) should be exposed 
                // while retaining the fire-and-forget behavior
                HandleException(e);
                throw;
            }
            finally
            {
                DisposeSocket(socket);
            }
        }


        protected int ExecuteInt(String key, RedisCommand command)
        {
            var val = ExecValue(key, command);
            if (Pipelining)
                return 0;
            return (int)val;
        }

        protected int ExecuteInt(IRedisNode node, RedisCommand command)
        {
            var val = ExecValue(node, command);
            if (Pipelining)
                return 0;
            return (int)val;
        }


        protected long ExecuteLong(IRedisNode node, RedisCommand command)
        {
            var val = ExecValue(node, command);
            if (Pipelining)
                return 0;
            return val;
        }


        protected bool ExecuteBool(IRedisNode node, RedisCommand command)
        {
            return ExecuteInt(node, command) > 0;
        }

        protected bool ExecuteBool(String key, RedisCommand command)
        {
            return ExecuteInt(key, command) > 0;
        }


        #endregion

        #region Miscellaneous // TODO: break out into helper class


        private static String ToString(byte[] value)
        {
            return (value == null) ? null : Encoding.UTF8.GetString(value);
        }

        #endregion 
        
        #region [ Disposable                  ]

        protected bool CheckDisposed(bool throwOnError)
        {
            if (throwOnError && Disposed)
                throw new ObjectDisposedException("RedisClient");

            return Disposed;
        }

        /// <summary>
        /// Releases all resources allocated by this instance
        /// </summary>
        /// <remarks>Technically it's not really neccesary to call this, since the client does not create "really" disposable objects, so it's safe to assume that when 
        /// the AppPool shuts down all resources will be released correctly and no handles or such will remain in the memory.</remarks>

        protected override void Release()
        {
            if (_serverPool == null)
                throw new ObjectDisposedException("RedisClient");
            try
            {
                _serverPool.Dispose();
            }
            finally
            {
                _serverPool = null;
            }
        }

		#endregion
    }
}
