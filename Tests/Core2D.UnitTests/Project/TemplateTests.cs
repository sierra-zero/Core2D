﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Xunit;

namespace Core2D.UnitTests
{
    public class TemplateTests
    {
        [Fact]
        public void Inherits_From_Container()
        {
            var target = new Template();
            Assert.True(target is Container);
        }
    }
}
