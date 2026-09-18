import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/widgets/board_card.dart';
import 'package:weaver/features/board/widgets/date_format.dart';

const double _laneLabelWidth = 160;
const double _columnWidth = 240;

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

  void _showMoveErrorIfAny() {
    final message = _viewModel.moveError;
    if (message == null) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    _viewModel.clearMoveError();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Weaver'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () => unawaited(widget.onLogout()),
          ),
        ],
      ),
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) {
          if (_viewModel.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }

          final loadError = _viewModel.loadError;
          if (loadError != null) {
            return _LoadErrorView(message: loadError, onRetry: _viewModel.retry);
          }

          return SingleChildScrollView(
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _BreadcrumbBar(
                    breadcrumbs: _viewModel.breadcrumbs,
                    onSelect: _viewModel.navigateToBreadcrumb,
                  ),
                  _SearchBar(
                    controller: _searchController,
                    onChanged: _viewModel.setSearchQuery,
                  ),
                  _TimeFilterBar(
                    start: _viewModel.filterStart,
                    end: _viewModel.filterEnd,
                    onChanged: _viewModel.setTimeFilter,
                    onClear: _viewModel.clearTimeFilter,
                  ),
                  _StatusFilterBar(
                    statuses: _viewModel.statuses,
                    hiddenStatusIds: _viewModel.hiddenStatusIds,
                    onToggle: _viewModel.toggleStatusVisibility,
                  ),
                  _SortBar(
                    value: _viewModel.sortOption,
                    onChanged: _viewModel.setSortOption,
                  ),
                  _StatusHeaderRow(statuses: _viewModel.visibleStatuses),
                  for (final lane in _viewModel.swimlanes)
                    _SwimlaneRow(
                      swimlane: lane,
                      statuses: _viewModel.visibleStatuses,
                      onCardDropped: _viewModel.moveCard,
                      onCardReparented: _viewModel.reparentCard,
                      onCardOpened: _viewModel.drillInto,
                      onCardRescheduled: _viewModel.rescheduleCard,
                      cardVisible: _viewModel.cardVisible,
                      cardComparator: _viewModel.cardComparator,
                    ),
                ],
              ),
            ),
          );
        },
      ),
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

    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
      child: Wrap(
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
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              )
            else
              TextButton(
                onPressed: () => unawaited(onSelect(i)),
                child: Text(breadcrumbs[i].title),
              ),
          ],
        ],
      ),
    );
  }
}

class _SearchBar extends StatelessWidget {
  const _SearchBar({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
      child: SizedBox(
        width: 320,
        child: TextField(
          controller: controller,
          onChanged: onChanged,
          decoration: const InputDecoration(
            isDense: true,
            prefixIcon: Icon(Icons.search),
            hintText: 'Search title or description',
            border: OutlineInputBorder(),
          ),
        ),
      ),
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
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
      child: Wrap(
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
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
      child: Row(
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
      ),
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
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 0),
      child: Wrap(
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
      ),
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

class _StatusHeaderRow extends StatelessWidget {
  const _StatusHeaderRow({required this.statuses});

  final List<BoardStatus> statuses;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const SizedBox(width: _laneLabelWidth),
        for (final status in statuses)
          SizedBox(
            width: _columnWidth,
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: Text(
                status.name,
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          ),
      ],
    );
  }
}

class _SwimlaneRow extends StatelessWidget {
  const _SwimlaneRow({
    required this.swimlane,
    required this.statuses,
    required this.onCardDropped,
    required this.onCardReparented,
    required this.onCardOpened,
    required this.onCardRescheduled,
    required this.cardVisible,
    required this.cardComparator,
  });

  final Swimlane swimlane;
  final List<BoardStatus> statuses;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final Future<void> Function(WorkItemCard card) onCardOpened;
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onCardRescheduled;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SizedBox(
              width: _laneLabelWidth,
              child: _SwimlaneLabel(
                swimlane: swimlane,
                onCardReparented: onCardReparented,
              ),
            ),
            for (final status in statuses)
              SizedBox(
                width: _columnWidth,
                child: _StatusColumn(
                  swimlane: swimlane,
                  status: status,
                  onCardDropped: onCardDropped,
                  onCardOpened: onCardOpened,
                  onCardRescheduled: onCardRescheduled,
                  cardVisible: cardVisible,
                  cardComparator: cardComparator,
                ),
              ),
          ],
        ),
      ),
    );
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
    required this.onCardOpened,
    required this.onCardRescheduled,
    required this.cardVisible,
    required this.cardComparator,
  });

  final Swimlane swimlane;
  final BoardStatus status;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(WorkItemCard card) onCardOpened;
  final Future<void> Function(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  )
  onCardRescheduled;
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
          constraints: const BoxConstraints(minHeight: 64),
          decoration: BoxDecoration(
            color: candidateData.isNotEmpty
                ? colorScheme.primaryContainer.withValues(alpha: 0.4)
                : colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          // A scroll view, not a plain Column, because IntrinsicHeight
          // (used to keep every status column in a row the same height)
          // can compute an intrinsic height a fraction of a pixel short of
          // what the column's own content needs — harmless when scrollable,
          // an overflow error otherwise. It also means a lane with many
          // cards scrolls instead of stretching the whole row.
          child: SingleChildScrollView(
            child: Column(
              children: [
                for (final card in cards)
                  BoardCard(
                    card: card,
                    onOpen: () => unawaited(onCardOpened(card)),
                    onReschedule: onCardRescheduled,
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}
