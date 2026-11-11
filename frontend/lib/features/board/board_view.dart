import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/backlog_view_model.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/board_section.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/repeating_items_view_model.dart';
import 'package:weaver/features/board/saved_views_view_model.dart';
import 'package:weaver/features/board/trash_view_model.dart';
import 'package:weaver/features/board/views/backlog/backlog_view.dart';
import 'package:weaver/features/board/views/board_tab.dart';
import 'package:weaver/features/board/views/header/done_filter_bar.dart';
import 'package:weaver/features/board/views/header/header_bar.dart';
import 'package:weaver/features/board/views/header/scheduled_filter_bar.dart';
import 'package:weaver/features/board/views/header/sort_bar.dart';
import 'package:weaver/features/board/views/header/space_filter_bar.dart';
import 'package:weaver/features/board/views/header/status_filter_bar.dart';
import 'package:weaver/features/board/views/header/tag_filter_bar.dart';
import 'package:weaver/features/board/views/header/widgets/view_menu.dart';
import 'package:weaver/features/board/views/header/widgets/view_menu_action.dart';
import 'package:weaver/features/board/views/header/widgets/view_name_dialog.dart';
import 'package:weaver/features/board/views/hierarchy/hierarchy_view.dart';
import 'package:weaver/features/board/views/repeating/repeating_view.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_view.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/views/trash/trash_view.dart';
import 'package:weaver/features/board/widgets/add_tags_dialog.dart';
import 'package:weaver/features/board/widgets/assign_dialog.dart';
import 'package:weaver/features/board/widgets/create_work_item_dialog.dart';
import 'package:weaver/features/board/widgets/space_section_header.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/navigation/admin_page_opener.dart';
import 'package:weaver/shared/navigation/work_item_detail_opener.dart';
import 'package:weaver/shared/widgets/confirm_action.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// Below this width, the header collapses to a column (app name on its own
/// row) and the swimlane board's status columns become individually
/// horizontally-scrollable — with the swimlane-label column pinned in
/// place — rather than stretching to fill the available width.
const double _narrowLayoutBreakpoint = 760;

const Duration _searchDebounceDelay = Duration(milliseconds: 200);

class BoardView extends StatefulWidget {
  const BoardView({
    required this.onLogout,
    required this.openWorkItemDetails,
    this.openAdmin,
    super.key,
  });

  final Future<void> Function() onLogout;
  final WorkItemDetailOpener openWorkItemDetails;

  /// Null for users who aren't admins, which hides the Admin button.
  final AdminPageOpener? openAdmin;

  @override
  State<BoardView> createState() => _BoardViewState();
}

class _BoardViewState extends State<BoardView>
    with SingleTickerProviderStateMixin {
  final BoardViewModel _viewModel = getIt<BoardViewModel>();
  final RepeatingItemsViewModel _repeatingViewModel =
      getIt<RepeatingItemsViewModel>();
  final TrashViewModel _trashViewModel = getIt<TrashViewModel>();
  final BacklogViewModel _backlogViewModel = getIt<BacklogViewModel>();
  final SavedViewsViewModel _savedViews = getIt<SavedViewsViewModel>();
  final TextEditingController _searchController = TextEditingController();
  Timer? _searchDebounce;
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
    unawaited(_load());
    _viewModel.addListener(_showMoveErrorIfAny);
    _savedViews.addListener(_showViewErrorIfAny);
    _tabController.addListener(_onTabChanged);
  }

  /// Opens the user's default View, in its layout.
  Future<void> _load() async {
    await (_viewModel.load(), _savedViews.load()).wait;
    if (!mounted) return;
    if (_viewModel.currentView case final view?) {
      _savedViews.knowDefault(view.id);
    }
    _showOpenView();
  }

  /// After a View opens: the search box shows its text and its layout's
  /// tab shows.
  void _showOpenView() {
    _searchController.text = _viewModel.searchQuery;
    final layout = _viewModel.currentView?.definition.layout;
    if (layout != null) _tabController.index = BoardTab.showing(layout).index;
    _loadBacklogIfShown();
  }

  Future<void> _openView(SavedView view) async {
    _searchDebounce?.cancel();
    await _viewModel.openView(view);
    if (mounted) _showOpenView();
  }

  Future<void> _focusSpace(Space space) async {
    await _viewModel.focusSpace(space);
    if (mounted) _loadBacklogIfShown();
  }

  Future<void> _setSpaces(Set<String> spaceIds) async {
    await _viewModel.setSpaces(spaceIds);
    if (mounted) _loadBacklogIfShown();
  }

  ViewLayout get _currentLayout => BoardTab.values[_tabController.index].layout;

  Future<void> _onViewAction(ViewMenuAction action) async {
    final current = _viewModel.currentView;
    switch (action) {
      case ViewMenuAction.saveAs:
        await _saveAsNewView();
      case ViewMenuAction.saveChanges when current != null:
        final saved = await _savedViews.update(
          current,
          _viewModel.definitionFor(current.name, _currentLayout),
        );
        if (saved != null) _viewModel.adoptView(saved);
      case ViewMenuAction.rename when current != null:
        await _renameView(current);
      case ViewMenuAction.makeDefault when current != null:
        await _savedViews.setDefault(current);
      case ViewMenuAction.delete when current != null:
        await _deleteView(current);
      case _:
        break;
    }
  }

  Future<void> _saveAsNewView() async {
    final name = await showViewNameDialog(
      context,
      title: 'Save as new View',
      confirmLabel: 'Save',
    );
    if (name == null) return;
    final saved = await _savedViews.create(
      _viewModel.definitionFor(name, _currentLayout),
    );
    if (saved != null) _viewModel.adoptView(saved);
  }

  Future<void> _renameView(SavedView view) async {
    final name = await showViewNameDialog(
      context,
      title: 'Rename View',
      confirmLabel: 'Rename',
      initialName: view.name,
    );
    if (name == null || name == view.name) return;
    final renamed = await _savedViews.rename(view, name);
    if (renamed != null) _viewModel.adoptView(renamed);
  }

  /// The View is shared, so deleting it removes it for everyone; whoever
  /// had it as their default gets Overview.
  Future<void> _deleteView(SavedView view) async {
    final confirmed = await confirmAction(
      context,
      title: 'Delete "${view.name}"?',
      message:
          'Views are shared, so it goes for everyone. Anyone who opens it '
          'by default gets Overview instead.',
      confirmLabel: 'Delete',
      isDestructive: true,
    );
    if (!confirmed || !await _savedViews.delete(view)) return;
    final overview = _savedViews.overview;
    if (overview != null && mounted) await _openView(overview);
  }

  void _showViewErrorIfAny() {
    final message = _savedViews.error;
    if (message == null) return;
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
    _savedViews.clearError();
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_showMoveErrorIfAny)
      ..dispose();
    _repeatingViewModel.dispose();
    _trashViewModel.dispose();
    _backlogViewModel.dispose();
    _savedViews
      ..removeListener(_showViewErrorIfAny)
      ..dispose();
    _searchDebounce?.cancel();
    _searchController.dispose();
    _tabController
      ..removeListener(_onTabChanged)
      ..dispose();
    super.dispose();
  }

  /// Filtering rebuilds every lane, so wait for a pause in typing. Clearing
  /// the box applies at once.
  void _onSearchChanged(String query) {
    _searchDebounce?.cancel();
    if (query.isEmpty) {
      _viewModel.setSearchQuery(query);
      return;
    }
    _searchDebounce = Timer(
      _searchDebounceDelay,
      () => _viewModel.setSearchQuery(query),
    );
  }

  void _onTabChanged() {
    setState(() {});
    // Reloaded on every visit: schedules can change, and items be deleted,
    // from any detail dialog.
    if (!_tabController.indexIsChanging) {
      if (_tabController.index == BoardTab.repeating.index) {
        unawaited(_repeatingViewModel.load());
      }
      if (_tabController.index == BoardTab.trash.index) {
        unawaited(_trashViewModel.load());
      }
      _loadBacklogIfShown();
    }
    final needsHierarchyData =
        _tabController.index == BoardTab.hierarchy.index ||
        _tabController.index == BoardTab.roadmap.index;
    if (needsHierarchyData && !_viewModel.hierarchyLoaded) {
      unawaited(_viewModel.loadHierarchy());
    }
  }

  /// Reloaded on every visit, like the Trash: items created or given dates
  /// elsewhere change what's in it.
  void _loadBacklogIfShown() {
    final spaces = _viewModel.shownSpaces;
    if (_tabController.index != BoardTab.backlog.index || spaces.isEmpty) {
      _backlogViewModel.stopRefreshing();
      return;
    }
    unawaited(_backlogViewModel.load(spaces));
  }

  /// Detail-dialog edits reach the board through the shared item store. The
  /// dialog only reports a change the store can't carry, such as repeat
  /// settings creating items, and that needs a reload.
  void _refreshAfterDetailEdits({bool reloadScope = true}) {
    if (reloadScope) unawaited(_viewModel.refreshCurrentScope());
    unawaited(_viewModel.refreshHierarchyIfLoaded());
  }

  Future<void> _openDetails(WorkItemCard card) async {
    var drilledIn = false;
    final changed = await widget.openWorkItemDetails(
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
    final changed = await widget.openWorkItemDetails(
      context,
      workItemId: workItemId,
    );
    if (changed) _refreshAfterDetailEdits();
  }

  /// Admins can rename or delete statuses and change accounts, none of which
  /// goes through [BoardViewModel], so everything shown is reloaded after.
  Future<void> _openAdmin(AdminPageOpener openAdmin) async {
    await openAdmin(context);
    if (!mounted) return;
    unawaited(_viewModel.refreshAfterAdminChanges());
  }

  Future<void> _openRepeatingItem(String workItemId) async {
    final changed = await widget.openWorkItemDetails(
      context,
      workItemId: workItemId,
    );
    if (!changed) return;
    _refreshAfterDetailEdits();
    unawaited(_repeatingViewModel.load());
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

  /// Opens the quick add-tags dialog for [workItemId] — used wherever a
  /// tag row's "+" is tapped (a card, a Hierarchy row or a Roadmap row).
  /// Tags already on the item aren't offered again.
  Future<void> _openAddTagsDialog(
    String workItemId,
    List<String> currentTags,
  ) async {
    final tags = await showAddTagsDialog(
      context,
      currentTags: currentTags,
      suggestions: _viewModel.availableTags,
    );
    if (tags == null || tags.isEmpty) return;
    await _viewModel.addTags(workItemId, tags);
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
            // Item edits don't notify the view model itself, but they can
            // add or drop a tag.
            listenable: Listenable.merge([_viewModel, _viewModel.itemChanges]),
            builder: (context, _) => Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              spacing: 8,
              children: [
                SpaceFilterBar(
                  spaces: _viewModel.spaces,
                  selectedIds: _viewModel.selectedSpaceIds,
                  onChanged: (ids) => unawaited(_setSpaces(ids)),
                ),
                ScheduledFilterBar(
                  only: _viewModel.scheduledOrInProgressOnly,
                  onChanged: _viewModel.setScheduledOrInProgressOnly,
                ),
                StatusFilterBar(
                  statuses: _viewModel.statuses,
                  hiddenStatusIds: _viewModel.hiddenStatusIds,
                  onToggle: _viewModel.toggleStatusVisibility,
                  spaceNameOf: _viewModel.spansSpaces
                      ? _viewModel.spaceNameOfStatus
                      : null,
                ),
                DoneFilterBar(
                  showDone: _viewModel.showDone,
                  hidingDays: _viewModel.currentSpace?.doneHidingDays,
                  onChanged: _viewModel.setShowDone,
                ),
                SortBar(
                  value: _viewModel.sortOption,
                  onChanged: _viewModel.setSortOption,
                ),
                TagFilterBar(
                  availableTags: _viewModel.availableTags,
                  isSelected: _viewModel.isTagFilterSelected,
                  onToggle: _viewModel.toggleTagFilter,
                  tagMatch: _viewModel.tagMatch,
                  onTagMatchChanged: _viewModel.setTagMatch,
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
                    viewMenu: ListenableBuilder(
                      listenable: _savedViews,
                      builder: (context, _) => ViewMenu(
                        views: _savedViews.views,
                        current: _viewModel.currentView,
                        defaultViewId: _savedViews.defaultViewId,
                        onOpen: (view) => unawaited(_openView(view)),
                        onAction: (action) => unawaited(_onViewAction(action)),
                      ),
                    ),
                    breadcrumbs: _viewModel.breadcrumbs,
                    onSelectBreadcrumb: _viewModel.navigateToBreadcrumb,
                    drillDownTargets: _viewModel.drillDownTargets,
                    onDrillDown: _viewModel.drillIntoScope,
                    searchController: _searchController,
                    onSearchChanged: _onSearchChanged,
                    filterStart: _viewModel.filterStart,
                    filterEnd: _viewModel.filterEnd,
                    onTimeFilterChanged: _viewModel.setTimeFilter,
                    onTimeFilterCleared: _viewModel.clearTimeFilter,
                    onOpenFilters: _openFiltersDialog,
                    onLogout: () => unawaited(widget.onLogout()),
                    onOpenAdmin: switch (widget.openAdmin) {
                      final openAdmin? => () => unawaited(
                        _openAdmin(openAdmin),
                      ),
                      null => null,
                    },
                    tabController: _tabController,
                  ),
                  Expanded(
                    child: TabBarView(
                      controller: _tabController,
                      children: [
                        _buildBoardArea(context),
                        // Drawn from every item, so any item edit can reorder
                        // or refilter them. Only built while their tab shows.
                        ListenableBuilder(
                          listenable: _viewModel.itemChanges,
                          builder: (context, _) => RoadmapView(
                            viewModel: _viewModel,
                            onItemOpened: _openDetailsById,
                            onAddTagsRequested: _openAddTagsDialog,
                            onSpaceTapped: (space) =>
                                unawaited(_focusSpace(space)),
                          ),
                        ),
                        BacklogView(
                          viewModel: _backlogViewModel,
                          spaces: _viewModel.shownSpaces,
                          onItemOpened: _openDetailsById,
                        ),
                        ListenableBuilder(
                          listenable: _viewModel.itemChanges,
                          builder: (context, _) => HierarchyView(
                            viewModel: _viewModel,
                            onItemOpened: _openDetailsById,
                            onAssignRequested: _openAssignDialog,
                            onAddTagsRequested: _openAddTagsDialog,
                            onSpaceTapped: (space) =>
                                unawaited(_focusSpace(space)),
                          ),
                        ),
                        RepeatingView(
                          viewModel: _repeatingViewModel,
                          onItemOpened: (id) =>
                              unawaited(_openRepeatingItem(id)),
                          onSchedulesChanged: _refreshAfterDetailEdits,
                        ),
                        TrashView(
                          viewModel: _trashViewModel,
                          onRestored: _refreshAfterDetailEdits,
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

    if (_viewModel.hasNoSpaces) {
      return Container(
        color: swimlaneBackground,
        alignment: Alignment.center,
        padding: const EdgeInsets.all(24),
        child: Text(
          widget.openAdmin == null
              ? 'There are no Spaces yet. Ask an admin to create one.'
              : 'There are no Spaces yet. Create one on the Admin page.',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.titleMedium,
        ),
      );
    }

    final sections = _viewModel.boardSections;
    return Container(
      width: double.infinity,
      color: swimlaneBackground,
      child: SingleChildScrollView(
        child: LayoutBuilder(
          builder: (context, constraints) => Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final section in sections) ...[
                if (sections.length > 1)
                  SpaceSectionHeader(
                    space: section.space,
                    onTap: () => unawaited(_focusSpace(section.space)),
                  ),
                _buildSection(section, constraints.maxWidth),
              ],
            ],
          ),
        ),
      ),
    );
  }

  /// One Space's lanes under its own status columns.
  Widget _buildSection(BoardSection section, double width) => SwimlaneView(
    width: width,
    statuses: section.statuses,
    swimlaneListenables: section.lanes,
    onCardDropped: _viewModel.moveCard,
    onCardReparented: _viewModel.reparentCard,
    onCardDetailsOpened: _openDetails,
    onSwimlaneLabelTapped: _openDetailsById,
    onAssignRequested: _openAssignDialog,
    onAddTagsRequested: _openAddTagsDialog,
    cardVisible: _viewModel.cardVisible,
    cardComparator: _viewModel.cardComparator,
    assigneeInitialFor: _viewModel.assigneeInitialFor,
    typeColorFor: _viewModel.typeColorFor,
    collapsedSwimlaneIds: _collapsedSwimlaneIds,
    onToggleSwimlaneCollapsed: _toggleSwimlaneCollapsed,
    onToggleAllSwimlanesCollapsed: _toggleAllSwimlanesCollapsed,
  );
}
