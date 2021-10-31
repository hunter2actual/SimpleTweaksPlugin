﻿using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using SimpleTweaksPlugin.Helper;
using SimpleTweaksPlugin.TweakSystem;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig : IPluginConfiguration {
        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        [NonSerialized]
        private SimpleTweaksPlugin plugin;

        public int Version { get; set; } = 3;

        public SimpleTweaksPluginConfig() { }

        public List<string> EnabledTweaks = new List<string>();

        public bool HideKofi;
        public bool ShowExperimentalTweaks;

        public bool ShowTweakDescriptions = true;
        public bool ShowTweakIDs;

        public string CustomCulture = string.Empty;
        public string Language = null;

        public void Init(SimpleTweaksPlugin plugin, DalamudPluginInterface pluginInterface) {
            this.plugin = plugin;
            this.pluginInterface = pluginInterface;
        }

        public void Save() {
            pluginInterface.SavePluginConfig(this);
        }

        [NonSerialized] private SubTweakManager setTab = null;
        [NonSerialized] private bool settingTab = false;
        [NonSerialized] private string searchInput = string.Empty;
        [NonSerialized] private string lastSearchInput = string.Empty;
        [NonSerialized] private List<BaseTweak> searchResults = new List<BaseTweak>();
        
        private void DrawTweakConfig(BaseTweak t, ref bool hasChange) {
            var enabled = t.Enabled;
            if (t.Experimental && !ShowExperimentalTweaks && !enabled) return;
            if (ImGui.Checkbox($"###{t.Key}enabledCheckbox", ref enabled)) {
                if (enabled) {
                    SimpleLog.Debug($"Enable: {t.Name}");
                    try {
                        t.Enable();
                        if (t.Enabled) {
                            EnabledTweaks.Add(t.Key);
                        }
                    } catch (Exception ex) {
                        plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                    }
                } else {
                    SimpleLog.Debug($"Disable: {t.Name}");
                    try {
                        t.Disable();
                    } catch (Exception ex) {
                        plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                    }
                    EnabledTweaks.RemoveAll(a => a == t.Key);
                }
                Save();
            }
            ImGui.SameLine();
            var descriptionX = ImGui.GetCursorPosX();
            if (!t.DrawConfig(ref hasChange)) {
                if (ShowTweakDescriptions && !string.IsNullOrEmpty(t.Description)) {
                    ImGui.SetCursorPosX(descriptionX);
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                    ImGui.TreeNodeEx(" ", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                    ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF888888);
                    var tweakDescription = t.LocString("Description", t.Description, "Tweak Description");
                    ImGui.TextWrapped($"{tweakDescription}");
                    ImGui.PopStyleColor();
                }
            }
            ImGui.Separator();
        }
        
        public bool DrawConfigUI() {
            var drawConfig = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            var windowFlags = ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(new Vector2(600 * scale, 200 * scale), new Vector2(800 * scale, 800 * scale));
            ImGui.Begin($"{plugin.Name} Config", ref drawConfig, windowFlags);
            
            var showbutton = plugin.ErrorList.Count != 0 || !HideKofi;
            var buttonText = plugin.ErrorList.Count > 0 ? $"{plugin.ErrorList.Count} Errors Detected" : "Support on Ko-fi";
            var buttonColor = (uint) (plugin.ErrorList.Count > 0 ? 0x000000FF : 0x005E5BFF);
            
            if (showbutton) {
                ImGui.SetNextItemWidth(-(ImGui.CalcTextSize(buttonText).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemSpacing.X));
            } else {
                ImGui.SetNextItemWidth(-1);
            }
            
            ImGui.InputTextWithHint("###tweakSearchInput", "Search...", ref searchInput, 100);

            if (showbutton) {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | buttonColor);

                if (ImGui.Button(buttonText, new Vector2(-1, ImGui.GetItemRectSize().Y))) {
                    if (plugin.ErrorList.Count == 0) {
                        Common.OpenBrowser("https://ko-fi.com/Caraxi");
                    } else {
                        plugin.ShowErrorWindow = true;
                    }
                }
                ImGui.PopStyleColor(3);
            }
            
            ImGui.Dummy(new Vector2(1, ImGui.GetStyle().WindowPadding.Y - ImGui.GetStyle().ItemSpacing.Y * 2));
            ImGui.Separator();

            if (!string.IsNullOrEmpty(searchInput)) {
                if (lastSearchInput != searchInput) {
                    lastSearchInput = searchInput;
                    searchResults = new List<BaseTweak>();
                    var searchValue = searchInput.ToLowerInvariant();
                    foreach (var t in plugin.Tweaks) {
                        if (t is SubTweakManager stm) {
                            if (!stm.Enabled) continue;
                            foreach (var st in stm.GetTweakList()) {
                                if (st.Name.ToLowerInvariant().Contains(searchValue) || st.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue)) || st.LocString("Name", st.Name, "Tweak Name").ToLowerInvariant().Contains(searchValue)) {
                                    searchResults.Add(st);
                                }
                            }
                            continue;
                        }
                        if (t.Name.ToLowerInvariant().Contains(searchValue) || t.Tags.Any(tag => tag.ToLowerInvariant().Contains(searchValue))|| t.LocString("Name", t.Name, "Tweak Name").ToLowerInvariant().Contains(searchValue)) {
                            searchResults.Add(t);
                        }
                    }
                    
                    searchResults = searchResults.OrderBy(t => t.Name).ToList();
                }

                ImGui.BeginChild("search_scroll", new Vector2(-1));
                
                foreach (var t in searchResults) {
                    DrawTweakConfig(t, ref changed);
                }
                
                ImGui.EndChild();
            } else {
                var flags = settingTab ? ImGuiTabBarFlags.AutoSelectNewTabs : ImGuiTabBarFlags.None;
                if (ImGui.BeginTabBar("tweakCategoryTabBar", flags)) {
                    if (settingTab && setTab == null) {
                        settingTab = false;
                    } else {
                        if (ImGui.BeginTabItem(Loc.Localize("General Tweaks", "General Tweaks", "General Tweaks Tab Header") + "###generalTweaksTab")) {
                            ImGui.BeginChild("generalTweaks", new Vector2(-1, -1), false);

                            // ImGui.Separator();
                            foreach (var t in plugin.Tweaks) {
                                if (t is SubTweakManager) continue;
                                DrawTweakConfig(t, ref changed);
                            }
                            
                            ImGui.EndChild();
                            ImGui.EndTabItem();
                        }
                    }
                    
                    foreach (var stm in plugin.Tweaks.Where(t => t is SubTweakManager stm && (t.Enabled || stm.AlwaysEnabled)).Cast<SubTweakManager>()) {
                        if (settingTab == false && setTab == stm) {
                            settingTab = true;
                            continue;
                        }

                        if (settingTab && setTab == stm) {
                            settingTab = false;
                            setTab = null;
                        }

                        var stmName = stm.LocString("Name", stm.Name, "Tweak Name");
                        if (ImGui.BeginTabItem($"{stmName}###tweakCategoryTab_{stm.Key}")) {
                            ImGui.BeginChild($"{stm.Key}-scroll", new Vector2(-1, -1));
                            foreach (var tweak in stm.GetTweakList()) {
                                DrawTweakConfig(tweak, ref changed);
                            }
                            ImGui.EndChild();
                            ImGui.EndTabItem();
                        }
                    }

                    if (ImGui.BeginTabItem(Loc.Localize("General Options / TabHeader", "General Options") + $"###generalOptionsTab")) {
                        ImGui.BeginChild($"generalOptions-scroll", new Vector2(-1, -1));
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Experimental Tweaks", "Show Experimental Tweaks."), ref ShowExperimentalTweaks)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak Descriptions","Show tweak descriptions."), ref ShowTweakDescriptions)) Save();
                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Show Tweak IDs", "Show tweak IDs."), ref ShowTweakIDs)) Save();
                        ImGui.Separator();

                        if (Loc.DownloadError != null) {
                            ImGui.TextColored(new Vector4(1, 0, 0, 1), Loc.DownloadError.ToString());
                        }

                        if (Loc.LoadingTranslations) {
                            ImGui.Text("Downloading Translations...");
                        } else {
                            ImGui.SetNextItemWidth(130);
                            if (ImGui.BeginCombo(Loc.Localize("General Options / Language", "Language"), plugin.PluginConfig.Language)) {

                                if (ImGui.Selectable("en", Language == "en")) {
                                    Language = "en";
                                    plugin.SetupLocalization();
                                    Save();
                                }

                                #if DEBUG
                                if (ImGui.Selectable("DEBUG", Language == "DEBUG")) {
                                    Language = "DEBUG";
                                    plugin.SetupLocalization();
                                    Save();
                                }
                                #endif

                                var locDir = pluginInterface.GetPluginLocDirectory();

                                var locFiles = Directory.GetDirectories(locDir);

                                foreach (var f in locFiles) {
                                    var dir = new DirectoryInfo(f);
                                    if (ImGui.Selectable($"{dir.Name}##LanguageSelection", Language == dir.Name)) {
                                        Language = dir.Name;
                                        plugin.SetupLocalization();
                                        Save();
                                    }
                                }

                                ImGui.EndCombo();
                            }

                            ImGui.SameLine();

                            if (ImGui.SmallButton("Update Translations")) {
                                Loc.UpdateTranslations();
                            }

#if DEBUG
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Export Localizable")) {
                                try {
                                    ImGui.SetClipboardText(Loc.ExportLoadedDictionary());
                                } catch (Exception ex) {
                                    SimpleLog.Error(ex);
                                }
                            }
#endif
                        }

                        ImGui.Separator();

                        ImGui.SetNextItemWidth(130);
                        if (ImGui.BeginCombo(Loc.Localize("General Options / Formatting Culture", "Formatting Culture"), plugin.Culture.Name)) {

                            var cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
                            for (var i = 0; i < cultures.Length; i++) {
                                var c = cultures[i];
                                if (ImGui.Selectable($"{c.Name}", Equals(c, plugin.Culture))) {
                                    CustomCulture = c.Name;
                                    plugin.Culture = c;
                                    Save();
                                }
                            }

                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        ImGui.TextDisabled("Changes number formatting, not all tweaks support this.");

                        ImGui.Separator();
                        if (ImGui.Checkbox(Loc.Localize("General Options / Hide KoFi", "Hide Ko-fi link."), ref HideKofi)) Save();
                        ImGui.Separator();

                        foreach (var t in plugin.Tweaks.Where(t => t is SubTweakManager).Cast<SubTweakManager>()) {
                            if (t.AlwaysEnabled) continue;
                            var enabled = t.Enabled;
                            if (t.Experimental && !ShowExperimentalTweaks && !enabled) continue;
                            if (ImGui.Checkbox($"###{t.GetType().Name}enabledCheckbox", ref enabled)) {
                                if (enabled) {
                                    SimpleLog.Debug($"Enable: {t.Name}");
                                    try {
                                        t.Enable();
                                        if (t.Enabled) {
                                            EnabledTweaks.Add(t.GetType().Name);
                                        }
                                    } catch (Exception ex) {
                                        plugin.Error(t, ex, false, $"Error in Enable for '{t.Name}'");
                                    }
                                } else {
                                    SimpleLog.Debug($"Disable: {t.Name}");
                                    try {
                                        t.Disable();
                                    } catch (Exception ex) {
                                        plugin.Error(t, ex, true, $"Error in Disable for '{t.Name}'");
                                    }
                                    EnabledTweaks.RemoveAll(a => a == t.GetType().Name);
                                }
                                Save();
                            }
                            ImGui.SameLine();
                            var stmName = t.LocString("Name", t.Name, "Tweak Name");
                            ImGui.TreeNodeEx($"Enable Category: {stmName}", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                            if (ImGui.IsItemClicked() && t.Enabled) {
                                setTab = t;
                                settingTab = false;
                            }
                            ImGui.Separator();
                        }

                        ImGui.EndChild();
                        ImGui.EndTabItem();
                    }
                    
                    ImGui.EndTabBar();
                }
            }
            
            ImGui.End();

            if (changed) {
                Save();
            }
            
            return drawConfig;
        }
    }
}
