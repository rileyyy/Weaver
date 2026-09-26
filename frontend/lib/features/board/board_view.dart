import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/board_tab.dart';
import 'package:weaver/features/board/views/header/header_bar.dart';
import 'package:weaver/features/board/views/header/sort_bar.dart';
import 'package:weaver/features/board/views/header/status_filter_bar.dart';
import 'package:weaver/features/board/views/header/tag_filter_bar.dart';
import 'package:weaver/features/board/views/heirarchy/hierarchy_view.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_view.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/widgets/assign_dialog.dart';
import 'package:weaver/features/board/widgets/create_work_item_dialog.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';

/// Below this width, the header collapses to a column (app name on its own
/// row) and the swimlane board's status columns become individually
/// horizontally-scrollable — with the swimlane-label column pinned in
/// place — rather than stretching to fill the available width.
const double _narrowLayoutBreakpoint = 760;

class BoardView extends StatefulWidget {
  const BoardView({required this.onLogout, super.key});

  final Future<void> Function() onLogout;

  @override
  State<BoardView> createState() => _BoardViewState();
}

class _BoardViewState extends State<BoardView>
    with SingleTickerProviderStateMixin {
  final BoardViewModel _viewModel = getIt<BoardViewModel>();
  final TextEditingController _searchController = TextEditingController();
  late final TabController _tabController = TabController(
    length: BoardTab.values.length,
    vsync: this,
  );

  /// Swimlanes (keyed by their parent work item id) currently collapsed to a
  /// single-line label — purely view state, not persisted.
  final Set<String> _collapsedSwimlaneIds = {};

  void _toggleSwimlaneCollapsed(String parentId) {
    setState(() {
      if (!_collapsedSwimlaneIds.remove(parentId)) {
        _collapsedSwimlaneIds.add(parentId);
      }
    });
  }

  /// Collapses every swimlane if any are currently expanded, otherwise
  /// expands them all — the header row's "collapse/expand all" toggle.
  void _toggleAllSwimlanesCollapsed() {
    setState(() {
      final swimlanes = _viewModel.swimlanes;
      final allCollapsed =
          swimlanes.isNotEmpty &&
          swimlanes.every(
            (lane) => _collapsedSwimlaneIds.contains(lane.parentId),
          );
      if (allCollapsed) {
        _collapsedSwimlaneIds.clear();
      } else {
        _collapsedSwimlaneIds
          ..clear()
          ..addAll(swimlanes.map((lane) => lane.parentId));
      }
    });
  }

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
    _viewModel.addListener(_showMoveErrorIfAny);
    _tabController.addListener(_onTabChanged);
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_showMoveErrorIfAny)
      ..dispose();
    _searchController.dispose();
    _tabController
      ..removeListener(_onTabChanged)
      ..dispose();
    super.dispose();
  }

  void _onTabChanged() {
    setState(() {});
    final needsHierarchyData =
        _tabController.index == BoardTab.hierarchy.index ||
        _tabController.index == BoardTab.roadmap.index;
    if (needsHierarchyData && !_viewModel.hierarchyLoaded) {
      unawaited(_viewModel.loadHierarchy());
    }
  }

  /// Detail-dialog edits never go through [BoardViewModel], so reload what
  /// the board shows once the dialog reports a change.
  void _refreshAfterDetailEdits({bool reloadScope = true}) {
    if (reloadScope) unawaited(_viewModel.refreshCurrentScope());
    unawaited(_viewModel.refreshHierarchyIfLoaded());
  }

  Future<void> _openDetails(WorkItemCard card) async {
    var drilledIn = false;
    final changed = await showWorkItemDetailDialog(
      context,
      workItemId: card.id,
      onDrillInto: () {
        drilledIn = true;
        unawaited(_viewModel.drillInto(card));
      },
    );
    // A drill-in already loads fresh data for the new scope; refreshing the
    // old scope now would be the newer load and cancel it.
    if (changed) _refreshAfterDetailEdits(reloadScope: !drilledIn);
  }

  /// Opens a work item's details given just its id — used where there's no
  /// [WorkItemCard] in hand, e.g. tapping a swimlane's own label or a
  /// Hierarchy row.
  Future<void> _openDetailsById(String workItemId) async {
    final changed = await showWorkItemDetailDialog(context, workItemId: workItemId);
    if (changed) _refreshAfterDetailEdits();
  }

  /// Opens a picker to assign [workItemId] — used wherever an assignee
  /// avatar is tapped (a card, a swimlane label, or a Hierarchy row).
  /// Picking a user (or "Unassigned") assigns immediately; there's no
  /// separate confirm step.
  Future<void> _openAssignDialog(String workItemId, String? currentAssigneeId) {
    return showAssignDialog(
      context,
      users: _viewModel.users,
      currentAssigneeId: currentAssigneeId,
      onAssign: (userId) => _viewModel.assign(workItemId, userId),
    );
  }

  Future<void> _openCreateWorkItemDialog() async {
    final statuses = _viewModel.statuses;
    if (statuses.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No statuses are configured yet.')),
      );
      return;
    }

    final result = await showCreateWorkItemDialog(
      context,
      scopeParentId: _viewModel.breadcrumbs.last.id,
      swimlanes: _viewModel.swimlanes,
    );
    if (result == null) return;

    await _viewModel.createWorkItem(
      title: result.title,
      description: result.description,
      parentId: result.parentId,
      statusId: statuses.first.id,
    );
  }

  Future<void> _openFiltersDialog() {
    return showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Filters'),
        content: SizedBox(
          width: 360,
          child: ListenableBuilder(
            listenable: _viewModel,
            builder: (context, _) => Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              spacing: 8,
              children: [
                StatusFilterBar(
                  statuses: _viewModel.statuses,
                  hiddenStatusIds: _viewModel.hiddenStatusIds,
                  onToggle: _viewModel.toggleStatusVisibility,
                ),
                SortBar(
                  value: _viewModel.sortOption,
                  onChanged: _viewModel.setSortOption,
                ),
                TagFilterBar(
                  availableTags: _viewModel.availableTags,
                  selectedTags: _viewModel.selectedTagFilters,
                  onToggle: _viewModel.toggleTagFilter,
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Done'),
          ),
        ],
      ),
    );
  }

  void _showMoveErrorIfAny() {
    final message = _viewModel.moveError;
    if (message == null) return;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
    _viewModel.clearMoveError();
  }

  @override
  Widget build(BuildContext context) {
    final showAddButton = _tabController.index == BoardTab.swimLanes.index;
    return Scaffold(
      floatingActionButton: showAddButton
          ? ListenableBuilder(
              listenable: _viewModel,
              builder: (context, _) => _viewModel.canCreateWorkItem
                  ? FloatingActionButton(
                      onPressed: () => unawaited(_openCreateWorkItemDialog()),
                      tooltip: 'Add work item',
                      child: const Icon(Icons.add),
                    )
                  : const SizedBox.shrink(),
            )
          : null,
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) {
          return LayoutBuilder(
            builder: (context, constraints) {
              final isNarrow = constraints.maxWidth < _narrowLayoutBreakpoint;
              return Column(
                children: [
                  HeaderBar(
                    isNarrow: isNarrow,
                    breadcrumbs: _viewModel.breadcrumbs,
                    onSelectBreadcrumb: _viewModel.navigateToBreadcrumb,
                    searchController: _searchController,
                    onSearchChanged: _viewModel.setSearchQuery,
                    filterStart: _viewModel.filterStart,
                    filterEnd: _viewModel.filterEnd,
                    onTimeFilterChanged: _viewModel.setTimeFilter,
                    onTimeFilterCleared: _viewModel.clearTimeFilter,
                    onOpenFilters: _openFiltersDialog,
                    onLogout: () => unawaited(widget.onLogout()),
                    tabController: _tabController,
                  ),
                  Expanded(
                    child: TabBarView(
                      controller: _tabController,
                      children: [
                        _buildBoardArea(context),
                        RoadmapView(
                          viewModel: _viewModel,
                          onItemOpened: _openDetailsById,
                        ),
                        HierarchyView(
                          viewModel: _viewModel,
                          onItemOpened: _openDetailsById,
                          onAssignRequested: _openAssignDialog,
                        ),
                      ],
                    ),
                  ),
                ],
              );
            },
          );
        },
      ),
    );
  }

  Widget _buildBoardArea(BuildContext context) {
    // Anchored to surfaceContainerHighest (the same tone _HeaderBar uses)
    // rather than the theme's plain surface, then darkened rather than
    // lightened — the grid reads as a table/spreadsheet against a canvas as
    // dark as one of the status color swatches, with each status column and
    // card sitting on its own lighter surface on top of it.
    final swimlaneBackground = Theme.of(
      context,
    ).colorScheme.surfaceContainerHighest.darkenedBy(0.55);

    if (_viewModel.isLoading) {
      return Container(
        color: swimlaneBackground,
        child: const Center(child: CircularProgressIndicator()),
      );
    }

    final loadError = _viewModel.loadError;
    if (loadError != null) {
      return Container(
        color: swimlaneBackground,
        child: LoadErrorView(message: loadError, onRetry: _viewModel.retry),
      );
    }

    return Container(
      width: double.infinity,
      color: swimlaneBackground,
      child: SingleChildScrollView(
        child: LayoutBuilder(
          builder: (context, constraints) => SwimlaneView(
            width: constraints.maxWidth,
            statuses: _viewModel.visibleStatuses,
            swimlanes: _viewModel.swimlanes,
            onCardDropped: _viewModel.moveCard,
            onCardReparented: _viewModel.reparentCard,
            onCardDetailsOpened: _openDetails,
            onSwimlaneLabelTapped: _openDetailsById,
            onAssignRequested: _openAssignDialog,
            cardVisible: _viewModel.cardVisible,
            cardComparator: _viewModel.cardComparator,
            assigneeInitialFor: _viewModel.assigneeInitialFor,
            collapsedSwimlaneIds: _collapsedSwimlaneIds,
            onToggleSwimlaneCollapsed: _toggleSwimlaneCollapsed,
            onToggleAllSwimlanesCollapsed: _toggleAllSwimlanesCollapsed,
          ),
        ),
      ),
    );
  }
}
