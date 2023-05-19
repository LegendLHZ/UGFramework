// -----------------------------------------------------------------------
// <copyright file="SimpleObjPool.cs" company="AillieoTech">
// Copyright (c) AillieoTech. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace UnityEngine.UI
{
    using System;
    using System.Collections.Generic;

    public class SimpleObjPool<T>
    {
        private readonly Stack<T> stack;
        private readonly Func<T> ctor;
        private readonly Action<T> onRecycle;

        public SimpleObjPool(Action<T> onRecycle = null, Func<T> ctor = null)
        {
            stack = new Stack<T>();
            this.onRecycle = onRecycle;
            this.ctor = ctor;
        }

        public T Get()
        {
            T item;
            if (this.stack.Count == 0)
            {
                if (this.ctor != null)
                {
                    item = this.ctor();
                }
                else
                {
                    item = Activator.CreateInstance<T>();
                }
            }
            else
            {
                item = this.stack.Pop();
            }

            return item;
        }

        public void Recycle(T item)
        {
            if (this.onRecycle != null)
            {
                this.onRecycle.Invoke(item);
            }

            this.stack.Push(item);
        }

        public void Purge()
        {
            // TODO
        }
    }
}