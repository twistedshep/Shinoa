﻿// <copyright file="BadWordService.cs" company="The Shinoa Development Team">
// Copyright (c) 2016 - 2017 OmegaVesko.
// Copyright (c)        2017 The Shinoa Development Team.
// All rights reserved.
// Licensed under the MIT license.
// </copyright>

namespace Shinoa.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Databases;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Microsoft.EntityFrameworkCore;
    using static Databases.BadWordContext;

    public class BadWordService : IDatabaseService
    {
        private DbContextOptions dbOptions;

        private DiscordSocketClient client;

        public async Task<BindingStatus> AddBinding(bool global, ICommandContext context, string badWord)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                if (global)
                {
                    var badWordDbEntry = new ServerBadWord
                    {
                        Entry = badWord,
                        ServerId = context.Guild.Id,
                    };

                    if (await db.BadWordServerBindings.Include(b => b.BadWords).AnyAsync(b => b.ServerId == context.Guild.Id && b.IBadWords.Contains(badWordDbEntry)))
                        return BindingStatus.AlreadyExists;

                    if (!await db.BadWordServerBindings.AnyAsync(b => b.ServerId == context.Guild.Id))
                    {
                        var serverDbEntry = new BadWordServerBinding
                        {
                            ServerId = context.Guild.Id,
                            IBadWords = new List<IBadWord>
                            {
                                badWordDbEntry,
                            },
                        };
                        await db.BadWordServerBindings.AddAsync(serverDbEntry);
                    }
                    else
                    {
                        foreach (var server in db.BadWordServerBindings.Include(b => b.BadWords).Where(b => b.ServerId == context.Guild.Id))
                        {
                            db.BadWordServerBindings.Update(server);
                            server.IBadWords.Add(badWordDbEntry);
                        }
                    }

                    await db.SaveChangesAsync();
                    return BindingStatus.Added;
                }
                else
                {
                    var badWordDbEntry = new ChannelBadWord
                    {
                        Entry = badWord,
                        ChannelId = context.Channel.Id,
                        ServerId = context.Guild.Id,
                    };

                    if (await db.BadWordChannelBindings.Include(b => b.BadWords).AnyAsync(b => b.ChannelId == context.Guild.Id && b.IBadWords.Contains(badWordDbEntry)))
                        return BindingStatus.AlreadyExists;

                    if (!await db.BadWordChannelBindings.AnyAsync(b => b.ChannelId == context.Guild.Id))
                    {
                        var serverDbEntry = new BadWordChannelBinding
                        {
                            ChannelId = context.Guild.Id,
                            ServerId = context.Guild.Id,
                            IBadWords = new List<IBadWord>
                            {
                                badWordDbEntry,
                            },
                        };
                        await db.BadWordChannelBindings.AddAsync(serverDbEntry);
                    }
                    else
                    {
                        foreach (var channel in db.BadWordChannelBindings.Include(b => b.BadWords).Where(b => b.ChannelId == context.Channel.Id))
                        {
                            db.BadWordChannelBindings.Update(channel);
                            channel.IBadWords.Add(badWordDbEntry);
                        }
                    }

                    await db.SaveChangesAsync();
                    return BindingStatus.Added;
                }
            }
        }

        public async Task<BindingStatus> RemoveBinding(bool global, ICommandContext context, string badWord)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                if (global)
                {
                    var badWordDbEntry = new ServerBadWord
                    {
                        Entry = badWord,
                        ServerId = context.Guild.Id,
                    };

                    if (!await db.BadWordServerBindings.Include(b => b.BadWords).AnyAsync(b => b.ServerId == context.Guild.Id && b.IBadWords.Contains(badWordDbEntry)))
                        return BindingStatus.NotExisting;

                    foreach (var server in db.BadWordServerBindings.Include(b => b.BadWords).Where(b => b.ServerId == context.Guild.Id))
                    {
                        db.BadWordServerBindings.Update(server);
                        server.IBadWords.Remove(badWordDbEntry);
                    }

                    await db.SaveChangesAsync();
                    return BindingStatus.Removed;
                }
                else
                {
                    var badWordDbEntry = new ChannelBadWord
                    {
                        Entry = badWord,
                        ServerId = context.Guild.Id,
                        ChannelId = context.Channel.Id,
                    };

                    if (!await db.BadWordChannelBindings.Include(b => b.BadWords).AnyAsync(b => b.ChannelId == context.Guild.Id && b.IBadWords.Contains(badWordDbEntry)))
                        return BindingStatus.NotExisting;

                    foreach (var channel in db.BadWordChannelBindings.Include(b => b.BadWords).Where(b => b.ChannelId == context.Channel.Id))
                    {
                        db.BadWordChannelBindings.Update(channel);
                        channel.IBadWords.Remove(badWordDbEntry);
                    }

                    await db.SaveChangesAsync();
                    return BindingStatus.Removed;
                }
            }
        }

        public IDictionary<(IBadWordBinding entity, bool isGuild), IEnumerable<string>> ListBindings(ICommandContext context)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                IDictionary<(IBadWordBinding entity, bool isGuild), IEnumerable<string>> bindingList = new Dictionary<(IBadWordBinding entity, bool isGuild), IEnumerable<string>>();
                foreach (var server in db.BadWordServerBindings.Include(b => b.BadWords).Where(b => b.ServerId == context.Guild.Id))
                    bindingList.Add((server, true), server.IBadWords.Select(b => b.Entry).ToList());
                foreach (var channel in db.BadWordChannelBindings.Include(b => b.BadWords).Where(b => b.ServerId == context.Guild.Id))
                    bindingList.Add((channel, true), channel.IBadWords.Select(b => b.Entry).ToList());
                return bindingList;
            }
        }

        void IService.Init(dynamic config, IServiceProvider map)
        {
            dbOptions = map.GetService(typeof(DbContextOptions)) as DbContextOptions ?? throw new ServiceNotFoundException("Database options were not found in service provider.");
            client = map.GetService(typeof(DiscordSocketClient)) as DiscordSocketClient ?? throw new ServiceNotFoundException("Database context was not found in service provider.");

            client.MessageReceived += Handler;
        }

        private async Task Handler(SocketMessage arg)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                var toBeDeleted = false;
                var channelBindings = db.BadWordChannelBindings.Include(b => b.BadWords).Where(b => b.ChannelId == arg.Channel.Id);
                if (arg.Channel is ITextChannel guildChannel)
                {
                    var serverBindings = db.BadWordServerBindings.Include(b => b.BadWords).Where(b => b.ServerId == guildChannel.GuildId);

                    if (serverBindings.Any())
                    {
                        var badwords = serverBindings.SelectMany(b => b.IBadWords.Select(w => w.Entry));
                        if (badwords.Any(w => arg.Content.ToLowerInvariant().Contains(w.ToLowerInvariant())))
                            toBeDeleted = true;
                    }
                }

                if (channelBindings.Any())
                {
                    var badwords = channelBindings.SelectMany(b => b.IBadWords.Select(w => w.Entry));
                    if (badwords.Any(w => arg.Content.ToLowerInvariant().Contains(w.ToLowerInvariant())))
                        toBeDeleted = true;
                }

                if (toBeDeleted)
                    await arg.DeleteAsync();
            }
        }

        // Does both channel and server if applicable, because that's the only thing that makes sense.
        // TODO: Switch the interface to ICommandContext instead.
        async Task<bool> IDatabaseService.RemoveBinding(IEntity<ulong> binding)
        {
            var response = false;
            if (client.GetChannel(binding.Id) is ITextChannel channel)
            {
                response = await RemoveChannelBinding(channel);
            }

            if (client.GetGuild(binding.Id) is IGuild guild)
            {
                var response2 = await RemoveGuildBinding(guild);
                return response && response2;
            }
            else
            {
                return response;
            }
        }

        private async Task<bool> RemoveChannelBinding(ITextChannel channel)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                var bindings = await db.BadWordChannelBindings.Where(b => b.ChannelId == channel.Id).ToListAsync();
                if (!bindings.Any())
                    return false;
                foreach (var channelBinding in bindings)
                    db.Remove(channelBinding);
                await db.SaveChangesAsync();
                return true;
            }
        }

        private async Task<bool> RemoveGuildBinding(IGuild guild)
        {
            using (var db = new BadWordContext(dbOptions))
            {
                var bindings = await db.BadWordServerBindings.Where(b => b.ServerId == guild.Id).ToListAsync();
                if (!bindings.Any())
                    return false;
                foreach (var guildBinding in bindings)
                    db.Remove(guildBinding);
                await db.SaveChangesAsync();
                return true;
            }
        }
    }
}