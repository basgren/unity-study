using System;
using System.Collections.Generic;
using System.IO;
using Game.Editor.SceneTopology.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Read-only pannable / zoomable canvas that hosts scene cards and draws portal-link edges.
    /// Pan with middle-mouse drag or Alt+left drag. Zoom with the mouse wheel (centered on cursor).
    /// </summary>
    public sealed class SceneTopologyCanvas : VisualElement {
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 3f;
        private const float ZoomStep = 0.1f;
        private const float WorldHalfExtent = 20000f;

        private readonly VisualElement world;
        private readonly EdgesLayer edges;
        private readonly PinsLayer pins;
        private readonly VisualElement labels;
        private readonly Dictionary<string, SceneCard> cardsByGuid = new Dictionary<string, SceneCard>();

        private float zoom = 1f;
        private Vector2 translation;
        private bool panning;
        private Vector2 panStartMouse;
        private Vector2 panStartTranslation;
        private string hoveredGuid;

        private readonly List<PinInfo> pinInfos = new List<PinInfo>();
        private bool dragging;
        private PinInfo dragSource;
        private bool pinHovered;
        private Vector2 hoveredPinWorld;

        /// <summary>Pixels of slop added on top of <see cref="PinRadius"/> when hit-testing pins.</summary>
        private const float PinPickSlop = 3f;

        /// <summary>Called by the canvas when a portal link has been successfully written to disk.</summary>
        public Action OnLinkCreated;

        private static readonly Color EntranceEdgeColor = new Color(1f, 0.85f, 0.1f, 0.9f);
        private static readonly Color DoorEdgeColor = new Color(0.45f, 0.7f, 1f, 0.85f);
        // Violet, distinct from the gold Entrance edge; drawn solid like Entrance (not dashed like Door).
        private static readonly Color EntranceHorizontalEdgeColor = new Color(0.7f, 0.45f, 1f, 0.9f);
        private static readonly Color EntranceEdgeHoverColor = new Color(1f, 1f, 0.55f, 1f);
        private static readonly Color DoorEdgeHoverColor = new Color(0.75f, 0.9f, 1f, 1f);
        private static readonly Color EntranceHorizontalEdgeHoverColor = new Color(0.85f, 0.7f, 1f, 1f);
        private const float EntranceEdgeWidth = 2f;
        private const float DoorEdgeWidth = 1.5f;

        private const float PinRadius = 8f;
        private const float EntrancePinBorderWidth = 1f;
        private const float DoorPinBorderWidth = 2f;
        private static readonly Color EntrancePinFill = new Color(1f, 0.85f, 0.1f, 1f);
        private static readonly Color EntrancePinBorder = Color.black;
        private static readonly Color DoorPinFill = new Color(0.15f, 0.15f, 0.2f, 1f);
        private static readonly Color DoorPinBorder = new Color(0.45f, 0.7f, 1f, 1f);
        private static readonly Color EntranceHorizontalPinFill = new Color(0.7f, 0.45f, 1f, 1f);
        private static readonly Color EntranceHorizontalPinBorder = Color.black;
        private static readonly Color UnlinkedPinFill = new Color(1f, 0.25f, 0.25f, 1f);
        private static readonly Color UnlinkedPinBorder = new Color(0.55f, 0f, 0f, 1f);

        private static readonly Color DragLineColor = new Color(0.5f, 1f, 1f, 0.95f);
        private const float DragLineWidth = 2.5f;

        private static readonly Color PinHoverRingColor = new Color(1f, 1f, 1f, 0.95f);
        private const float PinHoverRingRadius = PinRadius + 2.5f;
        private const float PinHoverRingWidth = 1.5f;

        // Label colors picked per pin kind for readable contrast: gold entrance pins read best with
        // dark text; dark door pins and bright red unlinked pins read best with light text. Duplicate
        // labels override to a hot color so the authoring bug pops regardless of pin fill.
        private static readonly Color PinLabelOnLightColor = new Color(0f, 0f, 0f, 0.95f);
        private static readonly Color PinLabelOnDarkColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color PinLabelDuplicateColor = new Color(1f, 0.35f, 0.35f, 1f);
        private const int PinLabelFontSize = 9;

        public SceneTopologyCanvas() {
            style.flexGrow = 1f;
            style.overflow = Overflow.Hidden;
            // Need to be focusable so wheel events fire even before a click.
            focusable = true;
            pickingMode = PickingMode.Position;

            // world is the transformable container. Cards and edges sit inside it.
            world = new VisualElement {
                name = "topology-world",
                pickingMode = PickingMode.Ignore,
            };
            world.style.position = Position.Absolute;
            world.style.left = 0f;
            world.style.top = 0f;
            world.style.width = 0f;
            world.style.height = 0f;
            // Without an explicit transform-origin, Unity defaults to center — easier to reason about top-left.
            world.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(0f, 0f));
            Add(world);

            edges = new EdgesLayer();
            edges.style.position = Position.Absolute;
            edges.style.left = -WorldHalfExtent;
            edges.style.top = -WorldHalfExtent;
            edges.style.width = WorldHalfExtent * 2f;
            edges.style.height = WorldHalfExtent * 2f;
            edges.pickingMode = PickingMode.Ignore;
            world.Add(edges);

            // Pin layer sits above edges so links visually terminate at the pin disc edge.
            // Painter2D draws exact circles centered on the portal world position — no box-model
            // off-by-border issues from positioning a bordered VisualElement.
            pins = new PinsLayer();
            pins.style.position = Position.Absolute;
            pins.style.left = -WorldHalfExtent;
            pins.style.top = -WorldHalfExtent;
            pins.style.width = WorldHalfExtent * 2f;
            pins.style.height = WorldHalfExtent * 2f;
            pins.pickingMode = PickingMode.Ignore;
            world.Add(pins);

            // Labels layer carries small ID text next to each pin. Sits topmost so duplicate-id
            // red labels are always visible. Uses the same world-extent offset as the other layers
            // so labels can be positioned with world coordinates + WorldHalfExtent.
            labels = new VisualElement {
                name = "topology-labels",
                pickingMode = PickingMode.Ignore,
            };
            labels.style.position = Position.Absolute;
            labels.style.left = -WorldHalfExtent;
            labels.style.top = -WorldHalfExtent;
            labels.style.width = WorldHalfExtent * 2f;
            labels.style.height = WorldHalfExtent * 2f;
            world.Add(labels);

            RegisterCallback<WheelEvent>(OnWheel);
            // Capture-phase MouseDown so pin hits can take priority over the card's right-click
            // context menu (cards otherwise StopPropagation in bubble phase before we run).
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        /// <summary>Removes existing content and rebuilds the canvas from a freshly scanned graph + layout.</summary>
        public void Populate(SceneTopologyGraph graph, SceneTopologyLayout.Result layout) {
            // Drop old cards but keep the persistent paint layers in place.
            cardsByGuid.Clear();
            hoveredGuid = null;
            for (var i = world.childCount - 1; i >= 0; i--) {
                var child = world[i];
                if (child != edges && child != pins && child != labels) {
                    world.RemoveAt(i);
                }
            }

            edges.Clear();
            edges.SetHovered(null);
            pins.Clear();
            pins.SetDragLine(false, default, default, default);
            pins.SetHoveredPin(false, default);
            labels.Clear();
            pinInfos.Clear();
            dragging = false;
            pinHovered = false;

            if (graph == null) {
                edges.MarkDirtyRepaint();
                return;
            }

            // Normalize positions so the leftmost / topmost card sits with a small margin from origin.
            var (offset, span) = ComputeOffsetAndSpan(graph, layout);

            for (var i = 0; i < graph.Nodes.Count; i++) {
                var node = graph.Nodes[i];
                if (!layout.Positions.TryGetValue(node.SceneGuid, out var pos)) {
                    continue;
                }

                var size = layout.CardSizes[node.SceneGuid];
                var card = new SceneCard(node, size);
                var p = pos + offset;
                card.style.left = p.x;
                card.style.top = p.y;
                world.Add(card);
                cardsByGuid[node.SceneGuid] = card;

                var hoverGuid = node.SceneGuid;
                card.RegisterCallback<MouseEnterEvent>(_ => SetHovered(hoverGuid));
                card.RegisterCallback<MouseLeaveEvent>(_ => SetHovered(null));
            }

            // Edge geometry: cache pin world positions on the canvas for fast Painter2D access.
            var nodesByGuid = new Dictionary<string, SceneNodeData>(graph.Nodes.Count);
            for (var i = 0; i < graph.Nodes.Count; i++) {
                nodesByGuid[graph.Nodes[i].SceneGuid] = graph.Nodes[i];
            }

            for (var i = 0; i < graph.Edges.Count; i++) {
                var e = graph.Edges[i];
                if (!nodesByGuid.TryGetValue(e.FromSceneGuid, out var fromNode)) {
                    continue;
                }

                if (!nodesByGuid.TryGetValue(e.ToSceneGuid, out var toNode)) {
                    continue;
                }

                if (!layout.Positions.TryGetValue(e.FromSceneGuid, out var fromCardPos)) {
                    continue;
                }

                if (!layout.Positions.TryGetValue(e.ToSceneGuid, out var toCardPos)) {
                    continue;
                }

                var fromPortal = FindPortal(fromNode, e.Kind, e.FromPortalId);
                var toPortal = FindPortal(toNode, e.Kind, e.ToPortalId);
                if (fromPortal == null || toPortal == null) {
                    continue;
                }

                var fromCardSize = layout.CardSizes[e.FromSceneGuid];
                var toCardSize = layout.CardSizes[e.ToSceneGuid];

                var fromPin = PinCanvasPosition(fromCardPos + offset, fromCardSize, fromNode.BoundsLocal.size, fromPortal.LocalPos);
                var toPin = PinCanvasPosition(toCardPos + offset, toCardSize, toNode.BoundsLocal.size, toPortal.LocalPos);

                edges.Add(new EdgeLine {
                    From = fromPin,
                    To = toPin,
                    FromGuid = e.FromSceneGuid,
                    ToGuid = e.ToSceneGuid,
                    Color = EdgeColorFor(e.Kind),
                    HoverColor = EdgeHoverColorFor(e.Kind),
                    Width = EdgeWidthFor(e.Kind),
                    Dashed = e.Kind == PortalKindRef.Door,
                });
            }

            // A portal is "linked" iff it appears as an endpoint in at least one resolved edge.
            // Anything that didn't resolve (empty target, missing scene, missing target portal)
            // never made it into graph.Edges, so set membership identifies the broken ones.
            var linkedPortals = new HashSet<string>();
            for (var i = 0; i < graph.Edges.Count; i++) {
                var e = graph.Edges[i];
                linkedPortals.Add(PortalKey(e.FromSceneGuid, e.Kind, e.FromPortalId));
                linkedPortals.Add(PortalKey(e.ToSceneGuid, e.Kind, e.ToPortalId));
            }

            // Duplicate-id detection: portals colliding on (scene, kind, id) within the same scene
            // are an authoring bug — Door / Entrance per-scene autoincrement should keep ids unique,
            // but a clone or a hand-edit can break that. Mark them red in the label layer.
            var idCounts = new Dictionary<string, int>();
            for (var i = 0; i < graph.Nodes.Count; i++) {
                var node = graph.Nodes[i];
                for (var p = 0; p < node.Portals.Count; p++) {
                    var portal = node.Portals[p];
                    var key = PortalKey(node.SceneGuid, portal.Kind, portal.Id);
                    idCounts.TryGetValue(key, out var n);
                    idCounts[key] = n + 1;
                }
            }

            // Pin dots: one disc per portal, world-positioned. Use the same PinLocalPixels helper as
            // the edge endpoints so the line termination and the pin center coincide exactly.
            for (var i = 0; i < graph.Nodes.Count; i++) {
                var node = graph.Nodes[i];
                if (!layout.Positions.TryGetValue(node.SceneGuid, out var nodePos)) {
                    continue;
                }

                var cardSize = layout.CardSizes[node.SceneGuid];
                var cardWorld = nodePos + offset;
                var boundsSize = node.BoundsLocal.size;
                if (boundsSize.x <= 0f || boundsSize.y <= 0f) {
                    continue;
                }

                for (var p = 0; p < node.Portals.Count; p++) {
                    var portal = node.Portals[p];
                    var pinWorld = cardWorld + SceneTopologyLayout.PinLocalPixels(cardSize, boundsSize, portal.LocalPos);
                    var key = PortalKey(node.SceneGuid, portal.Kind, portal.Id);
                    var linked = linkedPortals.Contains(key);
                    var duplicate = idCounts.TryGetValue(key, out var count) && count > 1;
                    pins.Add(MakePinDot(portal.Kind, pinWorld, linked));
                    pinInfos.Add(new PinInfo {
                        WorldPosition = pinWorld,
                        SceneGuid = node.SceneGuid,
                        ScenePath = node.ScenePath,
                        Kind = portal.Kind,
                        PortalId = portal.Id,
                    });
                    labels.Add(MakePinLabel(portal.Id, portal.Kind, pinWorld, linked, duplicate));
                }
            }

            // Cards are added after the persistent layers in the world child list, so they'd cover
            // them. Render order we want: cards (bottom) → edges → pins → labels (top).
            edges.BringToFront();
            pins.BringToFront();
            labels.BringToFront();

            edges.MarkDirtyRepaint();
            pins.MarkDirtyRepaint();
            FrameAll(span);
        }

        private static Label MakePinLabel(string portalId, PortalKindRef kind, Vector2 worldPos,
            bool linked, bool duplicate) {
            var label = new Label(portalId);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            // Anchor at the pin's center; translate -50% on both axes so the label is visually
            // centered on the pin regardless of how wide the rendered text is.
            label.style.left = worldPos.x + WorldHalfExtent;
            label.style.top = worldPos.y + WorldHalfExtent;
            label.style.translate = new StyleTranslate(
                new Translate(Length.Percent(-50f), Length.Percent(-50f)));
            label.style.color = PickPinLabelColor(kind, linked, duplicate);
            label.style.fontSize = PinLabelFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            return label;
        }

        private static Color PickPinLabelColor(PortalKindRef kind, bool linked, bool duplicate) {
            if (duplicate) {
                return PinLabelDuplicateColor;
            }

            // Unlinked pins are bright red; dark text works better than white on that fill.
            if (!linked) {
                return PinLabelOnLightColor;
            }

            // Entrance pin fill is gold (light) → dark label; Door fill is near-black → light label.
            return kind == PortalKindRef.Entrance ? PinLabelOnLightColor : PinLabelOnDarkColor;
        }

        // Edge styling per kind. Entrance and EntranceHorizontal are both solid placement-style links;
        // only Door edges are dashed. EntranceHorizontal carries its own violet so it reads distinctly
        // from gold Entrance edges.
        private static Color EdgeColorFor(PortalKindRef kind) {
            switch (kind) {
                case PortalKindRef.Door:
                    return DoorEdgeColor;
                case PortalKindRef.EntranceHorizontal:
                    return EntranceHorizontalEdgeColor;
                default:
                    return EntranceEdgeColor;
            }
        }

        private static Color EdgeHoverColorFor(PortalKindRef kind) {
            switch (kind) {
                case PortalKindRef.Door:
                    return DoorEdgeHoverColor;
                case PortalKindRef.EntranceHorizontal:
                    return EntranceHorizontalEdgeHoverColor;
                default:
                    return EntranceEdgeHoverColor;
            }
        }

        private static float EdgeWidthFor(PortalKindRef kind) {
            return kind == PortalKindRef.Door ? DoorEdgeWidth : EntranceEdgeWidth;
        }

        private static float PinBorderWidthFor(PortalKindRef kind) {
            return kind == PortalKindRef.Door ? DoorPinBorderWidth : EntrancePinBorderWidth;
        }

        private static PinDot MakePinDot(PortalKindRef kind, Vector2 worldPos, bool linked) {
            // Unlinked portals (no target set, or target couldn't be resolved) light up red so the
            // missing wiring is visible at a glance. Keep the kind's border width so entrances and
            // doors still differ in weight.
            if (!linked) {
                return new PinDot {
                    Position = worldPos,
                    Radius = PinRadius,
                    FillColor = UnlinkedPinFill,
                    BorderColor = UnlinkedPinBorder,
                    BorderWidth = PinBorderWidthFor(kind),
                };
            }

            if (kind == PortalKindRef.Entrance) {
                return new PinDot {
                    Position = worldPos,
                    Radius = PinRadius,
                    FillColor = EntrancePinFill,
                    BorderColor = EntrancePinBorder,
                    BorderWidth = EntrancePinBorderWidth,
                };
            }

            if (kind == PortalKindRef.EntranceHorizontal) {
                return new PinDot {
                    Position = worldPos,
                    Radius = PinRadius,
                    FillColor = EntranceHorizontalPinFill,
                    BorderColor = EntranceHorizontalPinBorder,
                    BorderWidth = EntrancePinBorderWidth,
                };
            }

            return new PinDot {
                Position = worldPos,
                Radius = PinRadius,
                FillColor = DoorPinFill,
                BorderColor = DoorPinBorder,
                BorderWidth = DoorPinBorderWidth,
            };
        }

        private static string PortalKey(string sceneGuid, PortalKindRef kind, string portalId) {
            return sceneGuid + "|" + (int)kind + "|" + portalId;
        }

        public void ResetView() {
            zoom = 1f;
            translation = Vector2.zero;
            ApplyTransform();
        }

        private void SetHovered(string guid) {
            if (hoveredGuid == guid) {
                return;
            }

            if (hoveredGuid != null && cardsByGuid.TryGetValue(hoveredGuid, out var prevCard)) {
                prevCard.SetHighlighted(false);
            }

            hoveredGuid = guid;
            if (guid != null && cardsByGuid.TryGetValue(guid, out var newCard)) {
                newCard.SetHighlighted(true);
            }

            edges.SetHovered(guid);
            edges.MarkDirtyRepaint();
        }

        private (Vector2 offset, Rect span) ComputeOffsetAndSpan(SceneTopologyGraph graph, SceneTopologyLayout.Result layout) {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < graph.Nodes.Count; i++) {
                var guid = graph.Nodes[i].SceneGuid;
                if (!layout.Positions.TryGetValue(guid, out var pos)) {
                    continue;
                }

                var size = layout.CardSizes[guid];
                if (pos.x < minX) {
                    minX = pos.x;
                }

                if (pos.y < minY) {
                    minY = pos.y;
                }

                if (pos.x + size.x > maxX) {
                    maxX = pos.x + size.x;
                }

                if (pos.y + size.y > maxY) {
                    maxY = pos.y + size.y;
                }
            }

            if (float.IsPositiveInfinity(minX)) {
                return (Vector2.zero, Rect.zero);
            }

            const float margin = 40f;
            var offset = new Vector2(margin - minX, margin - minY);
            var span = Rect.MinMaxRect(margin, margin, maxX + offset.x, maxY + offset.y);
            return (offset, span);
        }

        private void FrameAll(Rect span) {
            // After Populate runs, layout pass hasn't computed the canvas size yet — defer to next frame.
            schedule.Execute(() => {
                if (span.width <= 0f || span.height <= 0f || resolvedStyle.width <= 0f || resolvedStyle.height <= 0f) {
                    ApplyTransform();
                    return;
                }

                const float pad = 60f;
                var fitX = (resolvedStyle.width - pad) / span.width;
                var fitY = (resolvedStyle.height - pad) / span.height;
                zoom = Mathf.Clamp(Mathf.Min(fitX, fitY, 1f), MinZoom, MaxZoom);

                var contentCenter = span.center * zoom;
                var viewCenter = new Vector2(resolvedStyle.width * 0.5f, resolvedStyle.height * 0.5f);
                translation = viewCenter - contentCenter;
                ApplyTransform();
            }).ExecuteLater(1);
        }

        private static PortalData FindPortal(SceneNodeData node, PortalKindRef kind, string id) {
            for (var i = 0; i < node.Portals.Count; i++) {
                if (node.Portals[i].Kind == kind && node.Portals[i].Id == id) {
                    return node.Portals[i];
                }
            }

            return null;
        }

        private static Vector2 PinCanvasPosition(Vector2 cardPos, Vector2 cardSize, Vector2 boundsSize, Vector2 portalLocal) {
            return cardPos + SceneTopologyLayout.PinLocalPixels(cardSize, boundsSize, portalLocal);
        }

        private void ApplyTransform() {
            // VisualElement.transform was deprecated in Unity 6; use style.translate / style.scale.
            // transform-origin is pinned to top-left (see world creation), so scale still pivots
            // about (0,0) to match the zoom-to-cursor math in OnWheel.
            world.style.translate = new Translate(translation.x, translation.y);
            world.style.scale = new Scale(new Vector2(zoom, zoom));
        }

        private void OnWheel(WheelEvent evt) {
            var mouse = (Vector2)evt.localMousePosition;
            var direction = evt.delta.y > 0f ? -1f : 1f;
            var newZoom = Mathf.Clamp(zoom + (direction * ZoomStep * zoom), MinZoom, MaxZoom);
            if (Mathf.Approximately(newZoom, zoom)) {
                return;
            }

            var ratio = newZoom / zoom;
            translation = (mouse * (1f - ratio)) + (translation * ratio);
            zoom = newZoom;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt) {
            // Mid-drag, ignore secondary clicks — Escape or releasing the primary button ends a drag.
            if (dragging) {
                return;
            }

            // Plain left-click on a pin starts a drag-link operation.
            if (evt.button == 0 && !evt.altKey && !evt.ctrlKey && !evt.shiftKey) {
                var worldMouse = CanvasToWorld(this.WorldToLocal(evt.mousePosition));
                if (TryHitTestPin(worldMouse, out var hit)) {
                    BeginDrag(hit, worldMouse);
                    Focus();
                    evt.StopPropagation();
                    return;
                }
            }

            // Right-click on a pin → pin context menu (Remove Link). When no pin is hit, let the
            // event continue down to the card so its own context menu opens.
            if (evt.button == 1) {
                var worldMouse = CanvasToWorld(this.WorldToLocal(evt.mousePosition));
                if (TryHitTestPin(worldMouse, out var hit)) {
                    ShowPinContextMenu(hit);
                    evt.StopPropagation();
                }

                return;
            }

            // Middle drag or Alt+Left drag → pan.
            var isPanGesture = evt.button == 2 || (evt.button == 0 && evt.altKey);
            if (!isPanGesture) {
                return;
            }

            panning = true;
            panStartMouse = evt.mousePosition;
            panStartTranslation = translation;
            this.CaptureMouse();
            Focus();
            evt.StopPropagation();
        }

        private void ShowPinContextMenu(PinInfo pin) {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Remove Link"), false, () => RemovePinLink(pin));
            menu.ShowAsContext();
        }

        private void RemovePinLink(PinInfo pin) {
            var ok = SceneTopologyLinker.RemoveLink(
                new SceneTopologyLinker.PortalRef {
                    ScenePath = pin.ScenePath,
                    Kind = pin.Kind,
                    PortalId = pin.PortalId,
                },
                out var error);

            if (!ok) {
                EditorUtility.DisplayDialog("Scene Topology", "Failed to remove link:\n\n" + error, "OK");
                return;
            }

            // OnLinkCreated is the same callback the window uses to refresh after any link write —
            // re-scan picks up the cleared portal and recolors it red.
            OnLinkCreated?.Invoke();
        }

        private void OnMouseMove(MouseMoveEvent evt) {
            if (dragging) {
                var worldMouse = CanvasToWorld(this.WorldToLocal(evt.mousePosition));
                UpdateDragLine(worldMouse);
                // Highlight the pin under cursor as a drop-target preview while dragging.
                UpdatePinHover(worldMouse);
                evt.StopPropagation();
                return;
            }

            if (panning) {
                translation = panStartTranslation + ((Vector2)evt.mousePosition - panStartMouse);
                ApplyTransform();
                evt.StopPropagation();
                return;
            }

            // Idle move: hit-test pins so the cursor target gets a hover halo.
            var idleWorldMouse = CanvasToWorld(this.WorldToLocal(evt.mousePosition));
            UpdatePinHover(idleWorldMouse);
        }

        private void OnMouseUp(MouseUpEvent evt) {
            if (dragging) {
                var worldMouse = CanvasToWorld(this.WorldToLocal(evt.mousePosition));
                TryHitTestPin(worldMouse, out var drop);
                FinishDrag(drop);
                evt.StopPropagation();
                return;
            }

            if (!panning) {
                return;
            }

            panning = false;
            this.ReleaseMouse();
            evt.StopPropagation();
        }

        private void OnMouseLeave(MouseLeaveEvent evt) {
            if (panning) {
                panning = false;
                this.ReleaseMouse();
            }

            // Pin hover should not stick when the cursor exits the canvas.
            ClearPinHover();
        }

        private void OnKeyDown(KeyDownEvent evt) {
            if (evt.keyCode == KeyCode.Escape && dragging) {
                CancelDrag();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F) {
                ResetView();
                evt.StopPropagation();
            }
        }

        private void BeginDrag(PinInfo source, Vector2 worldMouse) {
            dragging = true;
            dragSource = source;
            pins.SetDragLine(true, source.WorldPosition, worldMouse, DragLineColor);
            pins.MarkDirtyRepaint();
            this.CaptureMouse();
        }

        private void UpdatePinHover(Vector2 worldMouse) {
            if (TryHitTestPin(worldMouse, out var hit)) {
                if (!pinHovered || hoveredPinWorld != hit.WorldPosition) {
                    pinHovered = true;
                    hoveredPinWorld = hit.WorldPosition;
                    pins.SetHoveredPin(true, hit.WorldPosition);
                    pins.MarkDirtyRepaint();
                }

                return;
            }

            ClearPinHover();
        }

        private void ClearPinHover() {
            if (!pinHovered) {
                return;
            }

            pinHovered = false;
            pins.SetHoveredPin(false, default);
            pins.MarkDirtyRepaint();
        }

        private void UpdateDragLine(Vector2 worldMouse) {
            pins.SetDragLine(true, dragSource.WorldPosition, worldMouse, DragLineColor);
            pins.MarkDirtyRepaint();
        }

        private void FinishDrag(PinInfo? drop) {
            // Reset state first so any dialog opened by the link write doesn't see us as still dragging.
            var src = dragSource;
            dragging = false;
            pins.SetDragLine(false, default, default, default);
            pins.MarkDirtyRepaint();
            ClearPinHover();
            this.ReleaseMouse();

            if (!drop.HasValue) {
                return;
            }

            var dst = drop.Value;
            if (!IsValidDrop(src, dst)) {
                return;
            }

            var srcScene = Path.GetFileNameWithoutExtension(src.ScenePath);
            var dstScene = Path.GetFileNameWithoutExtension(dst.ScenePath);
            var message = "Create symmetric link?\n\n" +
                $"{src.Kind} #{src.PortalId} in {srcScene}\n" +
                "  ↔\n" +
                $"{dst.Kind} #{dst.PortalId} in {dstScene}\n\n" +
                "Both scenes will be saved.";
            if (!EditorUtility.DisplayDialog("Scene Topology — Create Link", message, "Create", "Cancel")) {
                return;
            }

            var ok = SceneTopologyLinker.LinkSymmetric(
                new SceneTopologyLinker.PortalRef {
                    ScenePath = src.ScenePath, Kind = src.Kind, PortalId = src.PortalId,
                },
                new SceneTopologyLinker.PortalRef {
                    ScenePath = dst.ScenePath, Kind = dst.Kind, PortalId = dst.PortalId,
                },
                out var error);

            if (!ok) {
                EditorUtility.DisplayDialog("Scene Topology", "Failed to create link:\n\n" + error, "OK");
                return;
            }

            OnLinkCreated?.Invoke();
        }

        private void CancelDrag() {
            dragging = false;
            pins.SetDragLine(false, default, default, default);
            pins.MarkDirtyRepaint();
            ClearPinHover();
            this.ReleaseMouse();
        }

        private static bool IsValidDrop(PinInfo source, PinInfo target) {
            // Cannot link to itself, and only same-kind pairs make sense (entrance↔entrance, door↔door).
            if (source.Kind != target.Kind) {
                return false;
            }

            if (source.SceneGuid == target.SceneGuid &&
                string.Equals(source.PortalId, target.PortalId, StringComparison.Ordinal)) {
                return false;
            }

            return true;
        }

        private bool TryHitTestPin(Vector2 worldPos, out PinInfo hit) {
            hit = default;
            var threshold = PinRadius + PinPickSlop;
            var bestDistSq = threshold * threshold;
            var found = false;
            for (var i = 0; i < pinInfos.Count; i++) {
                var info = pinInfos[i];
                var dx = info.WorldPosition.x - worldPos.x;
                var dy = info.WorldPosition.y - worldPos.y;
                var distSq = (dx * dx) + (dy * dy);
                if (distSq <= bestDistSq) {
                    bestDistSq = distSq;
                    hit = info;
                    found = true;
                }
            }

            return found;
        }

        private Vector2 CanvasToWorld(Vector2 canvasLocal) {
            // Inverts ApplyTransform: world.position = translation, world.scale = zoom.
            if (zoom <= 0f) {
                return canvasLocal;
            }

            return (canvasLocal - translation) / zoom;
        }

        private struct PinInfo {
            public Vector2 WorldPosition;
            public string SceneGuid;
            public string ScenePath;
            public PortalKindRef Kind;
            public string PortalId;
        }

        private struct EdgeLine {
            public Vector2 From;
            public Vector2 To;
            public string FromGuid;
            public string ToGuid;
            public Color Color;
            public Color HoverColor;
            public float Width;
            public bool Dashed;
        }

        private sealed class EdgesLayer : VisualElement {
            private readonly List<EdgeLine> lines = new List<EdgeLine>();
            private string hoveredGuid;

            public EdgesLayer() {
                generateVisualContent += OnPaint;
            }

            public new void Clear() {
                lines.Clear();
            }

            public void Add(EdgeLine line) {
                lines.Add(line);
            }

            public void SetHovered(string guid) {
                hoveredGuid = guid;
            }

            private void OnPaint(MeshGenerationContext ctx) {
                if (lines.Count == 0) {
                    return;
                }

                var painter = ctx.painter2D;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;

                // Two passes so hovered edges always sit on top of the rest.
                for (var pass = 0; pass < 2; pass++) {
                    for (var i = 0; i < lines.Count; i++) {
                        var line = lines[i];
                        var connected = hoveredGuid != null
                            && (line.FromGuid == hoveredGuid || line.ToGuid == hoveredGuid);
                        if (pass == 0 ? connected : !connected) {
                            continue;
                        }

                        painter.strokeColor = connected ? line.HoverColor : line.Color;
                        painter.lineWidth = line.Width;

                        // Coordinates passed in are canvas-world; this layer's top-left sits at -WorldHalfExtent so we add.
                        var a = new Vector2(line.From.x + WorldHalfExtent, line.From.y + WorldHalfExtent);
                        var b = new Vector2(line.To.x + WorldHalfExtent, line.To.y + WorldHalfExtent);

                        if (line.Dashed) {
                            DrawDashed(painter, a, b);
                        } else {
                            painter.BeginPath();
                            painter.MoveTo(a);
                            painter.LineTo(b);
                            painter.Stroke();
                        }
                    }
                }
            }

            private static void DrawDashed(Painter2D painter, Vector2 from, Vector2 to) {
                const float dashLen = 8f;
                const float gapLen = 6f;
                var total = (to - from).magnitude;
                if (total < 0.001f) {
                    return;
                }

                var dir = (to - from) / total;
                var cursor = 0f;
                painter.BeginPath();
                while (cursor < total) {
                    var endCursor = Mathf.Min(cursor + dashLen, total);
                    painter.MoveTo(from + (dir * cursor));
                    painter.LineTo(from + (dir * endCursor));
                    cursor = endCursor + gapLen;
                }

                painter.Stroke();
            }
        }

        private struct PinDot {
            public Vector2 Position;
            public float Radius;
            public Color FillColor;
            public Color BorderColor;
            public float BorderWidth;
        }

        private sealed class PinsLayer : VisualElement {
            private readonly List<PinDot> dots = new List<PinDot>();

            private bool dragActive;
            private Vector2 dragFrom;
            private Vector2 dragTo;
            private Color dragColor;

            private bool hoverActive;
            private Vector2 hoverPosition;

            public PinsLayer() {
                generateVisualContent += OnPaint;
            }

            public new void Clear() {
                dots.Clear();
            }

            public void Add(PinDot dot) {
                dots.Add(dot);
            }

            /// <summary>
            /// Show or hide the rubber-band drag line that runs from the source pin to the cursor
            /// while the user is creating a portal link. Painted last so it sits above pin dots.
            /// </summary>
            public void SetDragLine(bool active, Vector2 from, Vector2 to, Color color) {
                dragActive = active;
                dragFrom = from;
                dragTo = to;
                dragColor = color;
            }

            /// <summary>
            /// Show or hide the hover ring drawn around the pin under the cursor. Indicates that
            /// the pin is interactive (draggable) and previews drop targets during a drag.
            /// </summary>
            public void SetHoveredPin(bool active, Vector2 position) {
                hoverActive = active;
                hoverPosition = position;
            }

            private void OnPaint(MeshGenerationContext ctx) {
                if (dots.Count == 0 && !dragActive && !hoverActive) {
                    return;
                }

                var painter = ctx.painter2D;
                for (var i = 0; i < dots.Count; i++) {
                    var dot = dots[i];
                    // Layer top-left sits at -WorldHalfExtent; shift coords so we paint in positive space.
                    var center = new Vector2(
                        dot.Position.x + WorldHalfExtent,
                        dot.Position.y + WorldHalfExtent);

                    painter.fillColor = dot.FillColor;
                    painter.BeginPath();
                    painter.Arc(center, dot.Radius, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.ClosePath();
                    painter.Fill();

                    if (dot.BorderWidth > 0f) {
                        // Stroke radius pulled inward by half the line width so the outer pixel of
                        // the border lands exactly on Radius — keeps the visible disc the same size
                        // regardless of border thickness.
                        painter.strokeColor = dot.BorderColor;
                        painter.lineWidth = dot.BorderWidth;
                        painter.BeginPath();
                        painter.Arc(center, dot.Radius - (dot.BorderWidth * 0.5f),
                            Angle.Degrees(0f), Angle.Degrees(360f));
                        painter.ClosePath();
                        painter.Stroke();
                    }
                }

                // Hover ring sits above the discs but below the drag line.
                if (hoverActive) {
                    var c = new Vector2(
                        hoverPosition.x + WorldHalfExtent,
                        hoverPosition.y + WorldHalfExtent);
                    painter.strokeColor = PinHoverRingColor;
                    painter.lineWidth = PinHoverRingWidth;
                    painter.BeginPath();
                    painter.Arc(c, PinHoverRingRadius, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.ClosePath();
                    painter.Stroke();
                }

                if (dragActive) {
                    var a = new Vector2(dragFrom.x + WorldHalfExtent, dragFrom.y + WorldHalfExtent);
                    var b = new Vector2(dragTo.x + WorldHalfExtent, dragTo.y + WorldHalfExtent);
                    painter.strokeColor = dragColor;
                    painter.lineWidth = DragLineWidth;
                    painter.lineCap = LineCap.Round;
                    painter.BeginPath();
                    painter.MoveTo(a);
                    painter.LineTo(b);
                    painter.Stroke();
                }
            }
        }
    }
}
