﻿// <copyright file="OwinServerFactoryAttributeTests.cs" company="Microsoft Open Technologies, Inc.">
// Copyright 2011-2013 Microsoft Open Technologies, Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Owin.Host.MonoHttpListener.Tests
{
    public class OwinServerFactoryAttributeTests
    {
        private readonly Func<IDictionary<string, object>, Task> _notImplemented = env => { throw new NotImplementedException(); };

        [Fact]
        public void InitializeNullProperties_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => OwinServerFactoryAttribute.Initialize(null));
        }

        [Fact]
        public void Initialize_PopulatesExpectedFields()
        {
            var properties = new Dictionary<string, object>();
            OwinServerFactoryAttribute.Initialize(properties);

            Assert.Equal("1.0", properties["owin.Version"]);
            Assert.IsType(typeof(OwinHttpListener), properties["Owin.Host.MonoHttpListener.OwinHttpListener"]);
            Assert.IsType(typeof(Mono.Net.HttpListener), properties["Mono.Net.HttpListener"]);
        }

        [Fact]
        public void CreateNullAppFunc_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => OwinServerFactoryAttribute.Create(null, new Dictionary<string, object>()));
        }

        [Fact]
        public void CreateNullProperties_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => OwinServerFactoryAttribute.Create(_notImplemented, null));
        }

        [Fact]
        public void CreateEmptyProperties_Success()
        {
            OwinServerFactoryAttribute.Create(_notImplemented, new Dictionary<string, object>());
        }
    }
}
