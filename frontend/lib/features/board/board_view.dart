import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/features/board/widgets/create_work_item_dialog.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';

/// Below this width, the header collapses to a column (app name on its own
/// row) and the swimlane board's status columns become individually
/// horizontally-scrollable — with the swimlane-label column pinned in
/// place — rather than stretching to fill the available width.
const double _narrowLayoutBreakpoint = 760;

const double _laneLabelWidth = 160;
const double _minColumnWidth = 240;
const double _headerRowHeight = 48;

/// A collapsed swimlane's row height — just enough for its label's single
/// line and the collapse toggle, matching the status header row's height.
const double _collapsedSwimlaneRowHeight = _headerRowHeight;

/// How much larger the status column headers and swimlane ("project")
/// labels render than the theme's base title styles — the swim-lane grid's
/// row/column headers, so they read clearly against the grid's darker
/// background.
const double _gridHeaderFontScale = 1.2;

/// Gap between cards in a status column's two-per-row grid, both between
/// columns and between rows of cards.
const double _cardGridSpacing = 8;

/// A status column cell's own margin + padding (each `EdgeInsets.all(4)`),
/// subtracted from its outer width/height to get the space actually left
/// for cards.
const double _statusColumnChrome = 16;

/// Horizontal space [_SwimlaneLabel] gives to its collapse chevron and
/// padding before its title even starts wrapping: left+right padding
/// (4+8), the chevron's own width (24), and the gap after it (4).
const double _swimlaneLabelChrome = 40;

class BoardView extends StatefulWidget {
  const BoardView({required this.onLogout, super.key});

  final Future<void> Function() onLogout;

  @override
  State<BoardView> createState() => _BoardViewState();
}

/// The board's alternate views, selected via the tabs in the top bar.
/// Calendar and Hierarchy are stubs for now — only Swim Lanes is wired up.
enum _BoardTab { swimLanes, calendar, hierarchy }

class _BoardViewState extends State<BoardView> with SingleTickerProviderStateMixin {
  final BoardViewModel _viewModel = getIt<BoardViewModel>();
  final TextEditingController _searchController = TextEditingController();
  late final TabController _tabController = TabController(
    length: _BoardTab.values.length,
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
      final allCollapsed = swimlanes.isNotEmpty &&
          swimlanes.every((lane) => _collapsedSwimlaneIds.contains(lane.parentId));
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
    if (_tabController.index == _BoardTab.hierarchy.index && !_viewModel.hierarchyLoaded) {
      unawaited(_viewModel.loadHierarchy());
    }
  }

  void _onWorkItemDeleted() {
    unawaited(_viewModel.retry());
    unawaited(_viewModel.refreshHierarchyIfLoaded());
  }

  void _openDetails(WorkItemCard card) {
    unawaited(showWorkItemDetailDialog(
      context,
      workItemId: card.id,
      onDrillInto: () => unawaited(_viewModel.drillInto(card)),
      onDeleted: _onWorkItemDeleted,
    ));
  }

  /// Opens a work item's details given just its id — used where there's no
  /// [WorkItemCard] in hand, e.g. tapping a swimlane's own label or a
  /// Hierarchy row.
  void _openDetailsById(String workItemId) {
    unawaited(showWorkItemDetailDialog(
      context,
      workItemId: workItemId,
      onDeleted: _onWorkItemDeleted,
    ));
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
              children: [
                _StatusFilterBar(
                  statuses: _viewModel.statuses,
                  hiddenStatusIds: _viewModel.hiddenStatusIds,
                  onToggle: _viewModel.toggleStatusVisibility,
                ),
                const SizedBox(height: 8),
                _SortBar(
                  value: _viewModel.sortOption,
                  onChanged: _viewModel.setSortOption,
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
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    _viewModel.clearMoveError();
  }

  @override
  Widget build(BuildContext context) {
    final showAddButton = _tabController.index == _BoardTab.swimLanes.index;
    return Scaffold(
      floatingActionButton: showAddButton
          ? FloatingActionButton(
              onPressed: () => unawaited(_openCreateWorkItemDialog()),
              tooltip: 'Add work item',
              child: const Icon(Icons.add),
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
                  _HeaderBar(
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
                        const _CalendarViewStub(),
                        _HierarchyView(
                          viewModel: _viewModel,
                          onItemOpened: _openDetailsById,
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
    final swimlaneBackground =
        Theme.of(context).colorScheme.surfaceContainerHighest.darkenedBy(0.55);

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
        child: _LoadErrorView(message: loadError, onRetry: _viewModel.retry),
      );
    }

    return Container(
      width: double.infinity,
      color: swimlaneBackground,
      child: SingleChildScrollView(
        child: LayoutBuilder(
          builder: (context, constraints) =>
              _SwimlaneBoard(
                width: constraints.maxWidth,
                statuses: _viewModel.visibleStatuses,
                swimlanes: _viewModel.swimlanes,
                onCardDropped: _viewModel.moveCard,
                onCardReparented: _viewModel.reparentCard,
                onCardDetailsOpened: _openDetails,
                onSwimlaneLabelTapped: _openDetailsById,
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

class _HeaderBar extends StatelessWidget {
  const _HeaderBar({
    required this.isNarrow,
    required this.breadcrumbs,
    required this.onSelectBreadcrumb,
    required this.searchController,
    required this.onSearchChanged,
    required this.filterStart,
    required this.filterEnd,
    required this.onTimeFilterChanged,
    required this.onTimeFilterCleared,
    required this.onOpenFilters,
    required this.onLogout,
    required this.tabController,
  });

  final bool isNarrow;
  final List<ScopeCrumb> breadcrumbs;
  final Future<void> Function(int index) onSelectBreadcrumb;
  final TextEditingController searchController;
  final ValueChanged<String> onSearchChanged;
  final DateTime? filterStart;
  final DateTime? filterEnd;
  final void Function({DateTime? start, DateTime? end}) onTimeFilterChanged;
  final VoidCallback onTimeFilterCleared;
  final VoidCallback onOpenFilters;
  final VoidCallback onLogout;
  final TabController tabController;

  @override
  Widget build(BuildContext context) {
    final title = Text(
      'Weaver',
      style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.bold,
          ),
    );

    final navigationStack = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        _BreadcrumbBar(breadcrumbs: breadcrumbs, onSelect: onSelectBreadcrumb),
        _TimeFilterBar(
          start: filterStart,
          end: filterEnd,
          onChanged: onTimeFilterChanged,
          onClear: onTimeFilterCleared,
        ),
      ],
    );

    final actionsRow = _SearchAndActions(
      controller: searchController,
      onSearchChanged: onSearchChanged,
      onOpenFilters: onOpenFilters,
      onLogout: onLogout,
    );

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        border: Border(
          bottom: BorderSide(color: Theme.of(context).colorScheme.outlineVariant),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          isNarrow
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Center(child: title),
                    const SizedBox(height: 8),
                    navigationStack,
                    const SizedBox(height: 8),
                    actionsRow,
                  ],
                )
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: navigationStack),
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: title,
                    ),
                    Expanded(
                      child: Align(
                        alignment: Alignment.topRight,
                        child: actionsRow,
                      ),
                    ),
                  ],
                ),
          const SizedBox(height: 4),
          TabBar(
            controller: tabController,
            isScrollable: isNarrow,
            tabAlignment: isNarrow ? TabAlignment.start : TabAlignment.fill,
            tabs: const [
              Tab(text: 'Swim Lanes'),
              Tab(text: 'Calendar'),
              Tab(text: 'Hierarchy'),
            ],
          ),
        ],
      ),
    );
  }
}

class _CalendarViewStub extends StatelessWidget {
  const _CalendarViewStub();

  @override
  Widget build(BuildContext context) {
    return const Center(child: Text('Calendar view — coming soon'));
  }
}

/// Columns the Hierarchy view shows to the right of each row's title, in
/// order. Currently fixed to just [status]; a future milestone can make
/// this user-configurable (e.g. persisted per-user) — that's why this is a
/// small enum of builder specs rather than a single hard-coded status
/// column baked into the row widget itself.
enum HierarchyColumn {
  status;

  String label(BoardViewModel viewModel, HierarchyItem item) => switch (this) {
        HierarchyColumn.status => viewModel.statusNameFor(item.statusId) ?? '',
      };

  Color? color(BoardViewModel viewModel, HierarchyItem item) => switch (this) {
        HierarchyColumn.status => viewModel.statusColorFor(item.statusId),
      };
}

const List<HierarchyColumn> _hierarchyColumns = [HierarchyColumn.status];
const double _hierarchyColumnWidth = 140;
const double _hierarchyIndentPerLevel = 24;

/// Every work item nested under its parent, respecting the same time/search/
/// status filters and sort order as the swim-lane board (see
/// [BoardViewModel.hierarchyRoots]). Loaded lazily by [_BoardViewState] the
/// first time this tab is opened.
class _HierarchyView extends StatefulWidget {
  const _HierarchyView({required this.viewModel, required this.onItemOpened});

  final BoardViewModel viewModel;
  final void Function(String workItemId) onItemOpened;

  @override
  State<_HierarchyView> createState() => _HierarchyViewState();
}

class _HierarchyViewState extends State<_HierarchyView> {
  final Set<String> _collapsedIds = {};

  @override
  Widget build(BuildContext context) {
    final viewModel = widget.viewModel;

    if (viewModel.isHierarchyLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    final error = viewModel.hierarchyLoadError;
    if (error != null) {
      return _LoadErrorView(message: error, onRetry: viewModel.loadHierarchy);
    }

    final rows = <_HierarchyRow>[];
    void flatten(List<HierarchyNode> nodes, int depth) {
      for (final node in nodes) {
        rows.add(_HierarchyRow(node: node, depth: depth));
        if (node.children.isNotEmpty && !_collapsedIds.contains(node.item.id)) {
          flatten(node.children, depth + 1);
        }
      }
    }

    flatten(viewModel.hierarchyRoots, 0);

    if (rows.isEmpty) {
      return const Center(child: Text('No work items match the current filters.'));
    }

    return ListView.builder(
      itemCount: rows.length,
      itemBuilder: (context, index) {
        final row = rows[index];
        final node = row.node;
        return _HierarchyItemTile(
          node: node,
          depth: row.depth,
          isCollapsed: _collapsedIds.contains(node.item.id),
          onToggleCollapsed: () => setState(() {
            if (!_collapsedIds.remove(node.item.id)) _collapsedIds.add(node.item.id);
          }),
          onTap: () => widget.onItemOpened(node.item.id),
          viewModel: viewModel,
        );
      },
    );
  }
}

/// One flattened row: a [HierarchyNode] paired with how deep it sits in the
/// (currently expanded) tree, for [ListView.builder] to render linearly.
class _HierarchyRow {
  const _HierarchyRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}

class _HierarchyItemTile extends StatelessWidget {
  const _HierarchyItemTile({
    required this.node,
    required this.depth,
    required this.isCollapsed,
    required this.onToggleCollapsed,
    required this.onTap,
    required this.viewModel,
  });

  final HierarchyNode node;
  final int depth;
  final bool isCollapsed;
  final VoidCallback onToggleCollapsed;
  final VoidCallback onTap;
  final BoardViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final hasChildren = node.children.isNotEmpty;

    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: EdgeInsets.only(
          left: depth * _hierarchyIndentPerLevel,
          top: 6,
          bottom: 6,
          right: 12,
        ),
        child: Row(
          children: [
            // Fixed height/width regardless of whether this row has a
            // caret — IconButton's default 48x48 minimum tap target would
            // otherwise make rows with children taller than leaf rows.
            SizedBox(
              width: 24,
              height: 24,
              child: hasChildren
                  ? IconButton(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      constraints: const BoxConstraints(),
                      visualDensity: VisualDensity.compact,
                      icon: Icon(isCollapsed ? Icons.chevron_right : Icons.expand_more),
                      tooltip: isCollapsed ? 'Expand' : 'Collapse',
                      onPressed: onToggleCollapsed,
                    )
                  : null,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(node.item.title, overflow: TextOverflow.ellipsis),
            ),
            for (final column in _hierarchyColumns)
              SizedBox(
                width: _hierarchyColumnWidth,
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    _StatusDot(color: column.color(viewModel, node.item)),
                    const SizedBox(width: 6),
                    Flexible(
                      child: Text(
                        column.label(viewModel, node.item),
                        textAlign: TextAlign.right,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _SearchAndActions extends StatelessWidget {
  const _SearchAndActions({
    required this.controller,
    required this.onSearchChanged,
    required this.onOpenFilters,
    required this.onLogout,
  });

  final TextEditingController controller;
  final ValueChanged<String> onSearchChanged;
  final VoidCallback onOpenFilters;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    final searchField = TextField(
      controller: controller,
      onChanged: onSearchChanged,
      decoration: const InputDecoration(
        isDense: true,
        prefixIcon: Icon(Icons.search),
        hintText: 'Search title or description',
        border: OutlineInputBorder(),
      ),
    );

    return Row(
      children: [
        // Expanded (rather than a fixed width) so this row fits whatever
        // space the header layout gives it — a fixed width overflowed on
        // narrower desktop/tablet widths where the header is still in its
        // wide, three-section row arrangement.
        Expanded(child: searchField),
        const SizedBox(width: 8),
        IconButton(
          icon: const Icon(Icons.filter_list),
          tooltip: 'Filters',
          onPressed: onOpenFilters,
        ),
        IconButton(
          icon: const Icon(Icons.logout),
          tooltip: 'Sign out',
          onPressed: onLogout,
        ),
      ],
    );
  }
}

class _LoadErrorView extends StatelessWidget {
  const _LoadErrorView({required this.message, required this.onRetry});

  final String message;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: () => unawaited(onRetry()),
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}

class _BreadcrumbBar extends StatelessWidget {
  const _BreadcrumbBar({required this.breadcrumbs, required this.onSelect});

  final List<ScopeCrumb> breadcrumbs;
  final Future<void> Function(int index) onSelect;

  @override
  Widget build(BuildContext context) {
    final lastIndex = breadcrumbs.length - 1;

    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        for (var i = 0; i < breadcrumbs.length; i++) ...[
          if (i > 0)
            Icon(
              Icons.chevron_right,
              size: 18,
              color: Theme.of(context).colorScheme.outline,
            ),
          if (i == lastIndex)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(
                breadcrumbs[i].title,
                style: Theme.of(context).textTheme.titleLarge,
              ),
            )
          else
            TextButton(
              onPressed: () => unawaited(onSelect(i)),
              child: Text(breadcrumbs[i].title),
            ),
        ],
      ],
    );
  }
}

class _StatusFilterBar extends StatelessWidget {
  const _StatusFilterBar({
    required this.statuses,
    required this.hiddenStatusIds,
    required this.onToggle,
  });

  final List<BoardStatus> statuses;
  final Set<String> hiddenStatusIds;
  final ValueChanged<String> onToggle;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Show columns', style: Theme.of(context).textTheme.bodySmall),
        for (final status in statuses)
          FilterChip(
            avatar: _StatusDot(color: status.color),
            label: Text(status.name),
            selected: !hiddenStatusIds.contains(status.id),
            onSelected: (_) => onToggle(status.id),
          ),
      ],
    );
  }
}

/// A small colored circle indicating a status's configured color — falls
/// back to a neutral outline color if a status has none (e.g. a test
/// fixture that doesn't care about color).
class _StatusDot extends StatelessWidget {
  const _StatusDot({required this.color});

  final Color? color;

  static const double _size = 10;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _size,
      height: _size,
      decoration: BoxDecoration(
        color: color ?? Theme.of(context).colorScheme.outlineVariant,
        shape: BoxShape.circle,
      ),
    );
  }
}

class _SortBar extends StatelessWidget {
  const _SortBar({required this.value, required this.onChanged});

  final CardSortOption value;
  final ValueChanged<CardSortOption> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text('Sort by', style: Theme.of(context).textTheme.bodySmall),
        const SizedBox(width: 8),
        DropdownButton<CardSortOption>(
          value: value,
          onChanged: (option) => option == null ? null : onChanged(option),
          items: [
            for (final option in CardSortOption.values)
              DropdownMenuItem(value: option, child: Text(option.label)),
          ],
        ),
      ],
    );
  }
}

class _TimeFilterBar extends StatelessWidget {
  const _TimeFilterBar({
    required this.start,
    required this.end,
    required this.onChanged,
    required this.onClear,
  });

  final DateTime? start;
  final DateTime? end;
  final void Function({DateTime? start, DateTime? end}) onChanged;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      crossAxisAlignment: WrapCrossAlignment.center,
      spacing: 8,
      children: [
        Text('Show items active', style: Theme.of(context).textTheme.bodySmall),
        _DateFilterButton(
          label: 'from',
          value: start,
          onPicked: (picked) => onChanged(start: picked, end: end),
        ),
        _DateFilterButton(
          label: 'to',
          value: end,
          onPicked: (picked) => onChanged(start: start, end: picked),
        ),
        if (start != null || end != null)
          TextButton(onPressed: onClear, child: const Text('Clear')),
      ],
    );
  }
}

class _DateFilterButton extends StatelessWidget {
  const _DateFilterButton({
    required this.label,
    required this.value,
    required this.onPicked,
  });

  final String label;
  final DateTime? value;
  final ValueChanged<DateTime?> onPicked;

  @override
  Widget build(BuildContext context) {
    return TextButton(
      onPressed: () => unawaited(_pick(context)),
      child: Text(value == null ? label : formatDate(value!)),
    );
  }

  Future<void> _pick(BuildContext context) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: value ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
    );
    if (picked != null) onPicked(picked);
  }
}

/// Grid lines for the swim-lane board's table/grid styling — a fixed, mid
/// contrast white rather than a theme-derived color, since the board's
/// background ([_BoardViewState._buildBoardArea]) is always dark regardless
/// of light/dark theme.
const Color _gridLineColor = Colors.white24;
const Color _onGridBackground = Colors.white;

/// The swimlane board itself: a pinned lane-label column on the left plus
/// a columns area on the right. When [width] is wide enough to give every
/// status column at least [_minColumnWidth] alongside the label column,
/// the columns stretch evenly to fill the remaining width and nothing
/// scrolls horizontally. Otherwise each column keeps [_minColumnWidth] and
/// only the columns area scrolls horizontally — the label column, being a
/// sibling outside that scroll view, stays in view.
class _SwimlaneBoard extends StatelessWidget {
  const _SwimlaneBoard({
    required this.width,
    required this.statuses,
    required this.swimlanes,
    required this.onCardDropped,
    required this.onCardReparented,
    required this.onCardDetailsOpened,
    required this.onSwimlaneLabelTapped,
    required this.cardVisible,
    required this.cardComparator,
    required this.assigneeInitialFor,
    required this.collapsedSwimlaneIds,
    required this.onToggleSwimlaneCollapsed,
    required this.onToggleAllSwimlanesCollapsed,
  });

  final double width;
  final List<BoardStatus> statuses;
  final List<Swimlane> swimlanes;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final void Function(String workItemId) onSwimlaneLabelTapped;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;
  final String? Function(String? userId) assigneeInitialFor;
  final Set<String> collapsedSwimlaneIds;
  final void Function(String parentId) onToggleSwimlaneCollapsed;
  final VoidCallback onToggleAllSwimlanesCollapsed;

  @override
  Widget build(BuildContext context) {
    final availableForColumns = width - _laneLabelWidth;
    final useFlexColumns = statuses.isNotEmpty &&
        availableForColumns >= statuses.length * _minColumnWidth;
    final columnOuterWidth =
        useFlexColumns ? availableForColumns / statuses.length : _minColumnWidth;
    final cardWidth =
        (columnOuterWidth - _statusColumnChrome - _cardGridSpacing) / 2;
    // Half of cardWidth, not cardWidth itself — cards no longer need to be
    // square; fitting more information per card matters more than the
    // shape, so they're shorter and squatter instead.
    final cardHeight = cardWidth * 0.5;

    final baseHeaderStyle = Theme.of(context).textTheme.titleMedium;
    final headerTextStyle = baseHeaderStyle?.copyWith(
      fontSize: (baseHeaderStyle.fontSize ?? 16) * _gridHeaderFontScale,
      color: _onGridBackground,
      fontWeight: FontWeight.w600,
    );

    final allSwimlanesCollapsed = swimlanes.isNotEmpty &&
        swimlanes.every((lane) => collapsedSwimlaneIds.contains(lane.parentId));

    final labelColumn = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _GridRowBox(
          height: _headerRowHeight,
          showBottomBorder: true,
          child: Center(
            child: IconButton(
              iconSize: 20,
              visualDensity: VisualDensity.compact,
              color: _onGridBackground,
              icon: Icon(allSwimlanesCollapsed ? Icons.unfold_more : Icons.unfold_less),
              tooltip: allSwimlanesCollapsed ? 'Expand all' : 'Collapse all',
              onPressed: onToggleAllSwimlanesCollapsed,
            ),
          ),
        ),
        for (final lane in swimlanes)
          _GridRowBox(
            height: _rowHeightFor(context, lane, cardHeight),
            showBottomBorder: true,
            child: _SwimlaneLabel(
              swimlane: lane,
              assigneeInitial: assigneeInitialFor(lane.assignedToUserId),
              isCollapsed: collapsedSwimlaneIds.contains(lane.parentId),
              onCardReparented: onCardReparented,
              onTapped: onSwimlaneLabelTapped,
              onToggleCollapsed: () => onToggleSwimlaneCollapsed(lane.parentId),
            ),
          ),
      ],
    );

    final headerRow = _GridRowBox(
      height: _headerRowHeight,
      showBottomBorder: true,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final status in statuses)
            _columnWrapper(
              flex: useFlexColumns,
              showRightBorder: status != statuses.last,
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(status.name, style: headerTextStyle),
                ),
              ),
            ),
        ],
      ),
    );

    final laneRows = [
      for (final lane in swimlanes)
        _GridRowBox(
          height: _rowHeightFor(context, lane, cardHeight),
          showBottomBorder: true,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final status in statuses)
                _columnWrapper(
                  flex: useFlexColumns,
                  showRightBorder: status != statuses.last,
                  child: _StatusColumn(
                    swimlane: lane,
                    status: status,
                    cardWidth: cardWidth,
                    cardHeight: cardHeight,
                    isCollapsed: collapsedSwimlaneIds.contains(lane.parentId),
                    onCardDropped: onCardDropped,
                    onCardDetailsOpened: onCardDetailsOpened,
                    cardVisible: cardVisible,
                    cardComparator: cardComparator,
                    assigneeInitialFor: assigneeInitialFor,
                  ),
                ),
            ],
          ),
        ),
    ];

    final columnsContent = Column(children: [headerRow, ...laneRows]);
    final columnsArea = useFlexColumns
        ? columnsContent
        : SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SizedBox(
              width: statuses.length * _minColumnWidth,
              child: columnsContent,
            ),
          );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: _laneLabelWidth, child: labelColumn),
        Expanded(child: columnsArea),
      ],
    );
  }

  /// A collapsed lane gets the taller of [_collapsedSwimlaneRowHeight] (room
  /// enough for a collapsed status cell's card-count text) or its label's
  /// own wrapped-title height (see [_swimlaneLabelMinHeight]) — a collapsed
  /// label still soft-wraps rather than eliding, so a long title can still
  /// need more than one line even while collapsed.
  ///
  /// An expanded lane gets the taller of: enough rows of [cardHeight] cards
  /// for the busiest status column in this lane (plus always at least one
  /// trailing empty slot, so a full grid never looks completely "closed" —
  /// dropping one more card always has visible room to land in; defaulting
  /// to a single row of two slots when the lane is empty, rather than
  /// reserving a taller 2x2 grid up front), or whatever the lane's own label
  /// needs to fit its (possibly wrapped) title and assignee avatar.
  double _rowHeightFor(BuildContext context, Swimlane lane, double cardHeight) {
    if (collapsedSwimlaneIds.contains(lane.parentId)) {
      final labelHeight = _swimlaneLabelMinHeight(context, lane, showsAvatar: false);
      return labelHeight > _collapsedSwimlaneRowHeight
          ? labelHeight
          : _collapsedSwimlaneRowHeight;
    }

    var maxCount = 0;
    for (final status in statuses) {
      final count = lane.cards
          .where((c) => c.statusId == status.id && cardVisible(c))
          .length;
      if (count > maxCount) maxCount = count;
    }

    final rows = ((maxCount + 1) / 2).ceil();
    final cardBasedHeight =
        rows * cardHeight + (rows - 1) * _cardGridSpacing + _statusColumnChrome;
    final labelHeight = _swimlaneLabelMinHeight(context, lane, showsAvatar: true);
    return cardBasedHeight > labelHeight ? cardBasedHeight : labelHeight;
  }

  /// The [_SwimlaneLabel]'s own minimum height: its top/bottom padding, plus
  /// its (possibly multi-line, wrapped) title, plus — when [showsAvatar] —
  /// the fixed gap and assignee avatar below it. Measured directly rather
  /// than guessed, since the label and the status-columns row it sits
  /// beside must always end up exactly the same height (see [_GridRowBox]).
  double _swimlaneLabelMinHeight(
    BuildContext context,
    Swimlane lane, {
    required bool showsAvatar,
  }) {
    final baseLabelStyle = Theme.of(context).textTheme.titleSmall;
    final labelStyle = baseLabelStyle?.copyWith(
      fontSize: (baseLabelStyle.fontSize ?? 14) * _gridHeaderFontScale,
    );
    final textPainter = TextPainter(
      text: TextSpan(text: lane.title, style: labelStyle),
      textDirection: TextDirection.ltr,
      textScaler: MediaQuery.textScalerOf(context),
    )..layout(maxWidth: _laneLabelWidth - _swimlaneLabelChrome);

    const topPadding = 16;
    const bottomPadding = 8;
    const gapBeforeAvatar = 24;
    // A small cushion on top of the measured text height — TextPainter's
    // layout outside the widget tree can land a pixel or so short of what
    // the actual Text widget renders (rounding, font metrics), and this is
    // a floor other content must never clip against.
    const measurementSafetyMargin = 4;
    var height = topPadding + textPainter.height + measurementSafetyMargin + bottomPadding;
    if (showsAvatar) height += gapBeforeAvatar + AssigneeAvatar.defaultSize;
    return height;
  }

  Widget _columnWrapper({
    required bool flex,
    required bool showRightBorder,
    required Widget child,
  }) {
    final content = showRightBorder
        ? DecoratedBox(
            decoration: const BoxDecoration(
              border: Border(right: BorderSide(color: _gridLineColor)),
            ),
            child: child,
          )
        : child;
    return flex
        ? Expanded(child: content)
        : SizedBox(width: _minColumnWidth, child: content);
  }
}

/// One row of the swim-lane grid (a status header row or a swimlane row),
/// giving it the fixed [height] every row shares and, when [showBottomBorder]
/// is set, the horizontal grid line that separates it from the row below —
/// shared by both the pinned label column and the scrollable columns area so
/// the lines stay aligned across both.
class _GridRowBox extends StatelessWidget {
  const _GridRowBox({
    required this.height,
    required this.showBottomBorder,
    this.child,
  });

  final double height;
  final bool showBottomBorder;
  final Widget? child;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      decoration: showBottomBorder
          ? const BoxDecoration(
              border: Border(bottom: BorderSide(color: _gridLineColor)),
            )
          : null,
      child: child,
    );
  }
}

class _SwimlaneLabel extends StatelessWidget {
  const _SwimlaneLabel({
    required this.swimlane,
    required this.assigneeInitial,
    required this.isCollapsed,
    required this.onCardReparented,
    required this.onTapped,
    required this.onToggleCollapsed,
  });

  final Swimlane swimlane;

  /// The swimlane's own ("project") work item's assignee, resolved by the
  /// view model — see [BoardCard.assigneeInitial].
  final String? assigneeInitial;

  final bool isCollapsed;

  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final void Function(String workItemId) onTapped;
  final VoidCallback onToggleCollapsed;

  @override
  Widget build(BuildContext context) {
    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId != swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardReparented(details.data, swimlane.parentId)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        final borderRadius = BorderRadius.circular(8);
        final baseLabelStyle = Theme.of(context).textTheme.titleSmall;
        final labelStyle = baseLabelStyle?.copyWith(
          fontSize: (baseLabelStyle.fontSize ?? 14) * _gridHeaderFontScale,
          color: _onGridBackground,
          fontWeight: FontWeight.w600,
          decoration: TextDecoration.underline,
        );
        return Material(
          color: candidateData.isNotEmpty
              ? colorScheme.primaryContainer.withValues(alpha: 0.4)
              : Colors.transparent,
          borderRadius: borderRadius,
          child: InkWell(
            borderRadius: borderRadius,
            onTap: () => onTapped(swimlane.parentId),
            child: Padding(
              // Top-aligned rather than centered in the row (see
              // crossAxisAlignment below), with extra top margin so the
              // title doesn't sit flush against the row's own top edge —
              // and left free to wrap to multiple lines instead of
              // eliding, now that it's not vertically centered to make
              // room for a taller label.
              padding: const EdgeInsets.only(left: 4, right: 8, top: 16, bottom: 8),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Fixed size regardless of collapse state — matches the
                  // Hierarchy view's own expand/collapse caret treatment.
                  SizedBox(
                    width: 24,
                    height: 24,
                    child: IconButton(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      constraints: const BoxConstraints(),
                      visualDensity: VisualDensity.compact,
                      color: _onGridBackground,
                      icon: Icon(isCollapsed ? Icons.chevron_right : Icons.expand_more),
                      tooltip: isCollapsed ? 'Expand' : 'Collapse',
                      onPressed: onToggleCollapsed,
                    ),
                  ),
                  const SizedBox(width: 4),
                  Expanded(
                    child: isCollapsed
                        ? Text(swimlane.title, style: labelStyle, softWrap: true)
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(swimlane.title, style: labelStyle, softWrap: true),
                              // ~24px between the title and its own
                              // assignee avatar below it, so they don't
                              // sit too close together.
                              const SizedBox(height: 24),
                              AssigneeAvatar(initial: assigneeInitial),
                            ],
                          ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class _StatusColumn extends StatelessWidget {
  const _StatusColumn({
    required this.swimlane,
    required this.status,
    required this.cardWidth,
    required this.cardHeight,
    required this.isCollapsed,
    required this.onCardDropped,
    required this.onCardDetailsOpened,
    required this.cardVisible,
    required this.cardComparator,
    required this.assigneeInitialFor,
  });

  final Swimlane swimlane;
  final BoardStatus status;

  /// Precomputed by [_SwimlaneBoard] (shared across every column so a
  /// swimlane's row height, also computed there, matches what actually
  /// renders here).
  final double cardWidth;
  final double cardHeight;

  final bool isCollapsed;

  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;
  final String? Function(String? userId) assigneeInitialFor;

  @override
  Widget build(BuildContext context) {
    if (isCollapsed) {
      final count = swimlane.cards
          .where((c) => c.statusId == status.id && cardVisible(c))
          .length;
      return Container(
        margin: const EdgeInsets.all(4),
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerLow,
          borderRadius: BorderRadius.circular(8),
        ),
        child: count == 0
            ? null
            : Text(
                count == 1 ? '1 card' : '$count cards',
                style: Theme.of(context).textTheme.bodySmall,
              ),
      );
    }

    final cards = swimlane.cards
        .where((c) => c.statusId == status.id && cardVisible(c))
        .toList();
    final comparator = cardComparator;
    if (comparator != null) cards.sort(comparator);

    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId == swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardDropped(details.data, status.id)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        return Container(
          margin: const EdgeInsets.all(4),
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          // The row's own height (computed by _SwimlaneBoard) already
          // reserves at least a 2x2 grid, growing for however many rows
          // this cell's card count actually needs — so this Wrap never
          // needs to scroll internally, it just fills the space given.
          child: Wrap(
            spacing: _cardGridSpacing,
            runSpacing: _cardGridSpacing,
            children: [
              for (final card in cards)
                ConstrainedBox(
                  // minHeight (not a fixed height) so a card can still grow
                  // for a long, wrapped title instead of clipping it.
                  constraints: BoxConstraints(
                    minWidth: cardWidth,
                    maxWidth: cardWidth,
                    minHeight: cardHeight,
                  ),
                  child: BoardCard(
                    card: card,
                    assigneeInitial: assigneeInitialFor(card.assignedToUserId),
                    onOpenDetails: () => onCardDetailsOpened(card),
                  ),
                ),
            ],
          ),
        );
      },
    );
  }
}
