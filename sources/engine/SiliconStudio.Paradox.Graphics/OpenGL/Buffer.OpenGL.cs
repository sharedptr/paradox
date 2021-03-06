﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGL 
using System;
using System.Runtime.InteropServices;
using SiliconStudio.Core;
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
using OpenTK.Graphics.ES30;
using BufferUsageHint = OpenTK.Graphics.ES30.BufferUsage;
#else
using OpenTK.Graphics.OpenGL;
#endif

namespace SiliconStudio.Paradox.Graphics
{
    public partial class Buffer
    {
        internal BufferTarget bufferTarget;
        internal BufferUsageHint bufferUsageHint;

#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
        // Special case: ConstantBuffer are faked with a byte array on OpenGL ES 2.0.
        internal IntPtr StagingData { get; set; }
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="description">The description.</param>
        /// <param name="bufferFlags">Type of the buffer.</param>
        /// <param name="viewFormat">The view format.</param>
        /// <param name="dataPointer">The data pointer.</param>
        protected Buffer(GraphicsDevice device, BufferDescription description, BufferFlags bufferFlags, PixelFormat viewFormat, IntPtr dataPointer)
            : base(device)
        {
            Description = description;
            BufferFlags = bufferFlags;
            ViewFormat = viewFormat;

            Recreate(dataPointer);
        }

        public void Recreate(IntPtr dataPointer)
        {
            if ((BufferFlags & BufferFlags.VertexBuffer) == BufferFlags.VertexBuffer)
            {
                bufferTarget = BufferTarget.ArrayBuffer;
            }
            else if ((BufferFlags & BufferFlags.IndexBuffer) == BufferFlags.IndexBuffer)
            {
                bufferTarget = BufferTarget.ElementArrayBuffer;
            }

            if ((BufferFlags & BufferFlags.ConstantBuffer) == BufferFlags.ConstantBuffer)
            {
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
                // Special case: ConstantBuffer are faked with a byte array on OpenGL ES 2.0.
                StagingData = Marshal.AllocHGlobal(Description.SizeInBytes);
#else
                bufferTarget = BufferTarget.UniformBuffer;
#endif
            }
            else if (Description.Usage == GraphicsResourceUsage.Dynamic)
            {
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
                // OpenGL ES might not always support MapBuffer (TODO: Use MapBufferOES if available)
                StagingData = Marshal.AllocHGlobal(Description.SizeInBytes);
#endif
            }

            Init(dataPointer);
        }

        /// <inheritdoc/>
        protected internal override bool OnRecreate()
        {
            base.OnRecreate();

            if (Description.Usage == GraphicsResourceUsage.Immutable
                || Description.Usage == GraphicsResourceUsage.Default)
                return false;

            Recreate(IntPtr.Zero);

            return true;
        }

        /// <inheritdoc/>
        protected override void Destroy()
        {
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
            if (StagingData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StagingData);
                StagingData = IntPtr.Zero;
            }
#endif

            using (GraphicsDevice.UseOpenGLCreationContext())
            {
                GL.DeleteBuffers(1, ref resourceId);
            }

            resourceId = 0;

            base.Destroy();
        }

        protected void Init(IntPtr dataPointer)
        {
#if SILICONSTUDIO_PARADOX_GRAPHICS_API_OPENGLES
            if ((Description.BufferFlags & BufferFlags.ConstantBuffer) == BufferFlags.ConstantBuffer
                || Description.Usage == GraphicsResourceUsage.Dynamic)
            {
                if (dataPointer != IntPtr.Zero)
                {
                    // Special case: ConstantBuffer are faked with a byte array on OpenGL ES 2.0.
                    unsafe
                    {
                        Utilities.CopyMemory(StagingData, dataPointer, Description.SizeInBytes);
                    }
                }
                return;
            }
#endif
            switch (Description.Usage)
            {
                case GraphicsResourceUsage.Default:
                case GraphicsResourceUsage.Immutable:
                    bufferUsageHint = BufferUsageHint.StaticDraw;
                    break;
                case GraphicsResourceUsage.Dynamic:
                case GraphicsResourceUsage.Staging:
                    bufferUsageHint = BufferUsageHint.DynamicDraw;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("description.Usage");
            }

            using (var creationContext = GraphicsDevice.UseOpenGLCreationContext())
            {
                // If we're on main context, unbind VAO before binding context.
                // It will be bound again on next draw.
                if (!creationContext.UseDeviceCreationContext)
                    GraphicsDevice.UnbindVertexArrayObject();

                GL.GenBuffers(1, out resourceId);
                GL.BindBuffer(bufferTarget, resourceId);
                GL.BufferData(bufferTarget, (IntPtr)Description.SizeInBytes, dataPointer, bufferUsageHint);
                GL.BindBuffer(bufferTarget, 0);
            }
        }
    }
} 
#endif 
