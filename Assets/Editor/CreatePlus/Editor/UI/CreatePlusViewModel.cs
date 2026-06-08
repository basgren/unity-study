using System;
using System.Collections.Generic;
using CreatePlus.Core;

namespace CreatePlus.UI {
    /// <summary>
    /// Builds a renderable model of the palette from the registry, settings and current search query.
    /// This is the bridge between the UI-independent core and any UI: it resolves favorites, recent,
    /// pinned, hidden and collapsed state into plain data that a view simply draws. It depends only on
    /// the core, never on IMGUI or UI Toolkit, so a future UI Toolkit window can reuse it unchanged.
    ///
    /// Groups form an arbitrarily deep tree: a curated top group (e.g. "UI / Text") may contain leaf
    /// commands plus nested subgroups that mirror Unity's familiar Create submenus (e.g. "TextMeshPro").
    /// </summary>
    public static class CreatePlusViewModel {
        /// <summary>One node in the group tree: a curated group (depth 0) or a nested submenu.</summary>
        public sealed class GroupNode {
            public string PanelName;
            public string Title;
            public string Key;
            public int Depth;
            public bool Collapsed;

            /// <summary>Leaf commands directly in this node (already filtered by search/hidden).</summary>
            public readonly List<CreatePlusCommand> Commands = new List<CreatePlusCommand>();

            /// <summary>Nested submenus under this node.</summary>
            public readonly List<GroupNode> SubGroups = new List<GroupNode>();

            /// <summary>Pinned leaf commands anywhere in this node's subtree (shown when collapsed).</summary>
            public readonly List<CreatePlusCommand> Pinned = new List<CreatePlusCommand>();

            /// <summary>Total leaf commands in this node's whole subtree.</summary>
            public int TotalCount;

            public bool HasVisibleContent {
                get { return TotalCount > 0; }
            }
        }

        /// <summary>One renderable panel (Project / Unity Common).</summary>
        public sealed class PanelView {
            public string Name;
            public readonly List<GroupNode> Groups = new List<GroupNode>();
        }

        /// <summary>The full renderable model for one frame.</summary>
        public sealed class Model {
            public readonly List<CreatePlusCommand> Favorites = new List<CreatePlusCommand>();
            public readonly List<CreatePlusCommand> Recent = new List<CreatePlusCommand>();
            public bool FavoritesCollapsed;
            public bool RecentCollapsed;
            public PanelView Project = new PanelView { Name = "Project" };
            public PanelView UnityCommon = new PanelView { Name = "Unity Common" };

            /// <summary>Flat, top-to-bottom list of currently visible commands for keyboard navigation.</summary>
            public readonly List<CreatePlusCommand> NavOrder = new List<CreatePlusCommand>();
        }

        // Default-collapsed policy lives here (it depends on the group hierarchy). Top groups not listed
        // default to expanded; every nested subgroup defaults to collapsed to keep the palette tidy.
        static readonly HashSet<string> DefaultCollapsedTopGroups = new HashSet<string> {
            "Unity Common/Animation / Audio / Timeline",
            "Unity Common/UI / Text",
            "Unity Common/Packages / Tools",
            "Unity Common/Advanced / Rare"
        };

        /// <summary>Builds the model for the given search query.</summary>
        /// <param name="query">Current search text; empty shows everything.</param>
        /// <param name="includeHidden">When true, hidden commands are included (the "show hidden" mode).</param>
        public static Model Build(string query, bool includeHidden) {
            var model = new Model();
            IReadOnlyList<CreatePlusCommand> all = CreatePlusCommandRegistry.Commands;
            bool searching = !string.IsNullOrWhiteSpace(query);

            model.FavoritesCollapsed = CreatePlusSettingsStore.IsGroupCollapsed("Quick Access/Favorites", false);
            model.RecentCollapsed = CreatePlusSettingsStore.IsGroupCollapsed("Quick Access/Recent", true);

            BuildFavorites(model);
            BuildRecent(model, query);

            BuildPanel(model.Project, "Project", ProjectGroupOrder, all, query, includeHidden);
            BuildPanel(model.UnityCommon, "Unity Common", UnityCommonGroupOrder, all, query, includeHidden);

            // Navigation order matches the visual top-to-bottom reading order across both columns.
            if (!model.FavoritesCollapsed) {
                model.NavOrder.AddRange(model.Favorites);
            }

            if (!model.RecentCollapsed) {
                model.NavOrder.AddRange(model.Recent);
            }

            AppendPanelNav(model.NavOrder, model.Project, searching);
            AppendPanelNav(model.NavOrder, model.UnityCommon, searching);

            return model;
        }

        static void BuildFavorites(Model model) {
            // Favorites stay visible during search (per design); resolve ids to existing commands.
            foreach (string id in CreatePlusSettingsStore.GetFavorites()) {
                CreatePlusCommand command = CreatePlusCommandRegistry.Find(id);
                if (command != null) {
                    model.Favorites.Add(command);
                }
            }
        }

        static void BuildRecent(Model model, string query) {
            foreach (string id in CreatePlusSettingsStore.GetRecent()) {
                CreatePlusCommand command = CreatePlusCommandRegistry.Find(id);
                if (command != null && CreatePlusCommandFilter.Matches(command, query)) {
                    model.Recent.Add(command);
                }
            }
        }

        static void BuildPanel(PanelView panel, string panelName, string[] groupOrder,
                               IReadOnlyList<CreatePlusCommand> all, string query, bool includeHidden) {
            foreach (string groupName in groupOrder) {
                var entries = new List<Entry>();
                for (int i = 0; i < all.Count; i++) {
                    CreatePlusCommand command = all[i];
                    if (command.PanelName != panelName || command.GroupName != groupName) {
                        continue;
                    }

                    if (!includeHidden && CreatePlusSettingsStore.IsHidden(command.Id)) {
                        continue;
                    }

                    if (!CreatePlusCommandFilter.Matches(command, query)) {
                        continue;
                    }

                    string[] path = command.SubGroupPath ?? Array.Empty<string>();
                    entries.Add(new Entry(command, path, 0));
                }

                string key = CreatePlusSettingsStore.GroupKey(panelName, groupName);
                GroupNode node = BuildNode(panelName, groupName, key, 0, entries);
                panel.Groups.Add(node);
            }
        }

        /// <summary>Recursively builds a group node from entries, partitioning by their submenu segment.</summary>
        static GroupNode BuildNode(string panelName, string title, string key, int depth, List<Entry> entries) {
            var node = new GroupNode {
                PanelName = panelName,
                Title = title,
                Key = key,
                Depth = depth
            };

            bool defaultCollapsed = depth == 0 ? DefaultCollapsedTopGroups.Contains(key) : true;
            node.Collapsed = CreatePlusSettingsStore.IsGroupCollapsed(key, defaultCollapsed);

            // Partition: leaves at this level vs. entries that descend into a submenu segment.
            var segmentOrder = new List<string>();
            var bySegment = new Dictionary<string, List<Entry>>();
            foreach (Entry entry in entries) {
                if (entry.Remaining == 0) {
                    node.Commands.Add(entry.Command);
                } else {
                    string segment = entry.Path[entry.Index];
                    if (!bySegment.TryGetValue(segment, out List<Entry> bucket)) {
                        bucket = new List<Entry>();
                        bySegment[segment] = bucket;
                        segmentOrder.Add(segment);
                    }

                    bucket.Add(new Entry(entry.Command, entry.Path, entry.Index + 1));
                }
            }

            foreach (string segment in segmentOrder) {
                GroupNode child = BuildNode(panelName, segment, key + "/" + segment, depth + 1, bySegment[segment]);
                if (child.TotalCount > 0) {
                    node.SubGroups.Add(child);
                }
            }

            node.TotalCount = node.Commands.Count;
            foreach (GroupNode child in node.SubGroups) {
                node.TotalCount += child.TotalCount;
            }

            // Pinned items bubble up from the whole subtree so they remain visible under a collapsed group.
            foreach (CreatePlusCommand command in node.Commands) {
                if (CreatePlusSettingsStore.IsPinned(command.Id)) {
                    node.Pinned.Add(command);
                }
            }

            foreach (GroupNode child in node.SubGroups) {
                node.Pinned.AddRange(child.Pinned);
            }

            return node;
        }

        static void AppendPanelNav(List<CreatePlusCommand> nav, PanelView panel, bool searching) {
            foreach (GroupNode group in panel.Groups) {
                AppendNodeNav(nav, group, searching);
            }
        }

        static void AppendNodeNav(List<CreatePlusCommand> nav, GroupNode node, bool searching) {
            if (node.TotalCount == 0) {
                return;
            }

            if (node.Collapsed && !searching) {
                nav.AddRange(node.Pinned);
            } else {
                nav.AddRange(node.Commands);
                foreach (GroupNode child in node.SubGroups) {
                    AppendNodeNav(nav, child, searching);
                }
            }
        }

        /// <summary>A command paired with its remaining submenu path during tree construction.</summary>
        sealed class Entry {
            public readonly CreatePlusCommand Command;
            public readonly string[] Path;
            public readonly int Index;

            public Entry(CreatePlusCommand command, string[] path, int index) {
                Command = command;
                Path = path;
                Index = index;
            }

            public int Remaining {
                get { return Path.Length - Index; }
            }
        }

        // Group display order per panel (mirrors the design layout).
        static readonly string[] ProjectGroupOrder = {
            "Game",
            "Content",
            "Pipeline"
        };

        static readonly string[] UnityCommonGroupOrder = {
            "Core",
            "2D / Level",
            "Graphics / Rendering",
            "Animation / Audio / Timeline",
            "UI / Text",
            "Packages / Tools",
            "Advanced / Rare"
        };
    }
}
