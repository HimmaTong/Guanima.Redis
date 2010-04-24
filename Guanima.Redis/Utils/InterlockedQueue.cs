﻿using System;
using System.Threading;

namespace Guanima.Redis.Utils
{
    /// <summary>
    /// Implements a non-locking queue.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class InterlockedQueue<T>
    {
        private Node _headNode;
        private Node _tailNode;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Guanima.Redis.Utils.InterlockedQueue"/> class.
        /// </summary>
        public InterlockedQueue()
        {
            Node node = new Node(default(T));

            _headNode = node;
            _tailNode = node;
        }

        /// <summary>
        /// Removes and returns the item at the beginning of the <see cref="T:InterlockedQueue"/>.
        /// </summary>
        /// <param name="value">The object that is removed from the beginning of the <see cref="T:InterlockedQueue"/>.</param>
        /// <returns><value>true</value> if an item was successfully dequeued; otherwise <value>false</value>.</returns>
        public bool Dequeue(out T value)
        {
            Node head;
            Node tail;
            Node next;

            while (true)
            {
                // read head
                head = _headNode;
                tail = _tailNode;
                next = head.Next;

                // Are head, tail, and next consistent?
                if (Object.ReferenceEquals(_headNode, head))
                {
                    // is tail falling behind
                    if (ReferenceEquals(head.Next, tail.Next))
                    {
                        // is the queue empty?
                        if (ReferenceEquals(next, null))
                        {
                            value = default(T);

                            // queue is empty and cannot dequeue
                            return false;
                        }

                        Interlocked.CompareExchange<Node>(
                            ref _tailNode,
                            next.Next,
                            tail);
                    }
                    else // No need to deal with tail
                    {
                        // read value before CAS otherwise another deque might try to free the next node
                        value = next.Value;

                        // try to swing the head to the next node
                        if (Interlocked.CompareExchange<Node>(
                                ref _headNode,
                                next,
                                head) == head)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="T:InterlockedQueue"/>.
        /// </summary>
        /// <param name="value">The item to be added to the <see cref="T:InterlockedQueue"/>. The value can be <value>null</value>.</param>
        public void Enqueue(T value)
        {
            // Allocate a new node from the free list
            Node valueNode = new Node(value);

            while (true)
            {
                Node tail = _tailNode;
                Node next = tail.Next;

                // are tail and next consistent
                if (ReferenceEquals(tail, _tailNode))
                {
                    // was tail pointing to the last node?
                    if (ReferenceEquals(next, null))
                    {
                        if (ReferenceEquals(
                            Interlocked.CompareExchange(ref tail.Next, valueNode, next),
                            next
                            )
                            )
                        {
                            Interlocked.CompareExchange(ref _tailNode, valueNode, tail);
                            break;
                        }
                    }
                    else // tail was not pointing to last node
                    {
                        // try to swing Tail to the next node
                        Interlocked.CompareExchange<Node>(ref _tailNode, next, tail);
                    }
                }
            }
        }

        #region [ Node                        ]
        private class Node
        {
            public T Value;
            public Node Next;

            public Node(T value)
            {
                this.Value = value;
            }
        }
        #endregion
    }
}

#region [ License information          ]
/* ************************************************************
 *
 * Copyright (c) Attila Kiskó, enyim.com
 *
 * This source code is subject to terms and conditions of 
 * Microsoft Permissive License (Ms-PL).
 * 
 * A copy of the license can be found in the License.html
 * file at the root of this distribution. If you can not 
 * locate the License, please send an email to a@enyim.com
 * 
 * By using this source code in any fashion, you are 
 * agreeing to be bound by the terms of the Microsoft 
 * Permissive License.
 *
 * You must not remove this notice, or any other, from this
 * software.
 *
 * ************************************************************/
#endregion