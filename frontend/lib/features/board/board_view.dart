import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/theme/app_theme.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view.dart';

/// Below this width, the header collapses to a column (app name on its own
/// row) and the swimlane board's status columns become individually
/// horizontally-scrollable — with the swimlane-label column pinned in
/// place — rather than stretching to fill the available width.
const double _narrowLayoutBreakpoint = 760;

const double _laneLabelWidth = 160;
const double _minColumnWidth = 240;
const double _headerRowHeight = 40;
const double _swimlaneRowHeight = 220;

class BoardView extends StatefulWidget {
  const BoardView({required this.onLogout, super.key});

  final Future<void> Function() onLogout;

  @override
  State<BoardView> createState() => _BoardViewState();
}

class _BoardViewState extends State<BoardView> {
  final BoardViewModel _viewModel = getIt<BoardViewModel>();
  final TextEditingController _searchController = TextEditingController();

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
    _viewModel.addListener(_showMoveErrorIfAny);
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_showMoveErrorIfAny)
      ..dispose();
    _searchController.dispose();
    super.dispose();
  }

  void _openDetails(WorkItemCard card) {
    unawaited(showWorkItemDetailDialog(
      context,
      workItemId: card.id,
      onDrillInto: () => unawaited(_viewModel.drillInto(card)),
    ));
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
    return Scaffold(
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
                  ),
                  Expanded(child: _buildBoardArea(context)),
                ],
              );
            },
          );
        },
      ),
    );
  }

  Widget _buildBoardArea(BuildContext context) {
    // Anchored to surfaceContainerHighest (the same tone _HeaderBar uses),
    // not the theme's plain surface — a seed-generated light-theme surface
    // is already so close to white that lightening it further by 20% is
    // imperceptible. surfaceContainerHighest has real headroom to lighten
    // in both light and dark theme, so the header/board contrast this is
    // meant to create actually shows up.
    final swimlaneBackground =
        Theme.of(context).colorScheme.surfaceContainerHighest.lightenedBy(0.2);

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
                onCardRescheduled: _viewModel.rescheduleCard,
                onCardDetailsOpened: _openDetails,
                cardVisible: _viewModel.cardVisible,
                cardComparator: _viewModel.cardComparator,
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
      child: isNarrow
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
            label: Text(status.name),
            selected: !hiddenStatusIds.contains(status.id),
            onSelected: (_) => onToggle(status.id),
          ),
      ],
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
    required this.onCardRescheduled,
    required this.onCardDetailsOpened,
    required this.cardVisible,
    required this.cardComparator,
  });

  final double width;
  final List<BoardStatus> statuses;
  final List<Swimlane> swimlanes;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onCardRescheduled;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;

  @override
  Widget build(BuildContext context) {
    final availableForColumns = width - _laneLabelWidth;
    final useFlexColumns = statuses.isNotEmpty &&
        availableForColumns >= statuses.length * _minColumnWidth;

    final labelColumn = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: _headerRowHeight),
        for (final lane in swimlanes)
          SizedBox(
            height: _swimlaneRowHeight,
            child: _SwimlaneLabel(
              swimlane: lane,
              onCardReparented: onCardReparented,
            ),
          ),
      ],
    );

    final headerRow = SizedBox(
      height: _headerRowHeight,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final status in statuses)
            _columnWrapper(
              flex: useFlexColumns,
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Text(
                  status.name,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ),
            ),
        ],
      ),
    );

    final laneRows = [
      for (final lane in swimlanes)
        SizedBox(
          height: _swimlaneRowHeight,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final status in statuses)
                _columnWrapper(
                  flex: useFlexColumns,
                  child: _StatusColumn(
                    swimlane: lane,
                    status: status,
                    onCardDropped: onCardDropped,
                    onCardRescheduled: onCardRescheduled,
                    onCardDetailsOpened: onCardDetailsOpened,
                    cardVisible: cardVisible,
                    cardComparator: cardComparator,
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

  Widget _columnWrapper({required bool flex, required Widget child}) {
    return flex
        ? Expanded(child: child)
        : SizedBox(width: _minColumnWidth, child: child);
  }
}

class _SwimlaneLabel extends StatelessWidget {
  const _SwimlaneLabel({required this.swimlane, required this.onCardReparented});

  final Swimlane swimlane;
  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;

  @override
  Widget build(BuildContext context) {
    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId != swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardReparented(details.data, swimlane.parentId)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        return Container(
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : null,
            borderRadius: BorderRadius.circular(8),
          ),
          padding: const EdgeInsets.all(8),
          child: Text(
            swimlane.title,
            style: Theme.of(context).textTheme.titleSmall,
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
    required this.onCardDropped,
    required this.onCardRescheduled,
    required this.onCardDetailsOpened,
    required this.cardVisible,
    required this.cardComparator,
  });

  final Swimlane swimlane;
  final BoardStatus status;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onCardRescheduled;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;

  @override
  Widget build(BuildContext context) {
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
          // The row's height is fixed (see _swimlaneRowHeight), so a lane
          // with more cards than fit scrolls within its own cell instead
          // of growing the row.
          child: SingleChildScrollView(
            child: Column(
              children: [
                for (final card in cards)
                  BoardCard(
                    card: card,
                    onReschedule: onCardRescheduled,
                    onOpenDetails: () => onCardDetailsOpened(card),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}
