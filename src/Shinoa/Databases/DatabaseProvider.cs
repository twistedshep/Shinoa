﻿// <copyright file="DatabaseProvider.cs" company="The Shinoa Development Team">
// Copyright (c) 2016 - 2017 OmegaVesko.
// Copyright (c)        2017 The Shinoa Development Team.
// Licensed under the MIT license.
// </copyright>

namespace Shinoa.Databases
{
    /// <summary>
    /// Enum of supported database providers.
    /// </summary>
    public enum DatabaseProvider
    {
        InMemory,
        SQLServer,
        PostgreSQL,
        MySQL,
    }
}