import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/agenda_view_model.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/models/agenda_group.dart';
import 'package:weaver/features/board/state/agenda_groups.dart';
import 'package:weaver/features/board/views/agenda/agenda_labels.dart';
import 'package:weaver/features/board/views/agenda/widgets/agenda_item_tile.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// The Agenda layout: the shown Spaces' dated items that match the header's
/// filters, as one list grouped under Overdue, Today and each later day
/// (see [groupAgenda]). Made for phones: one column, no horizontal scroll,
/// and pull to refresh. Its items load with the Hierarchy's.
class AgendaView extends StatelessWidget {
  const AgendaView({
    super.key,
    required this.boardViewModel,
    required this.viewModel,
    required this.onItemOpened,
  });

  final BoardViewModel boardViewModel;
  final AgendaViewModel viewModel;
  final void Function(String workItemId) onItemOpened;

  Future<void> _reload() async {
    await (viewModel.load(), boardViewModel.loadHierarchy()).wait;
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (boardViewModel.isHierarchyLoading || viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        final error = boardViewModel.hierarchyLoadError ?? viewModel.loadError;
        final today = viewModel.today;
        if (error != null || today == null) {
          return LoadErrorView(
            message:
                error ??
                'Could not load the Agenda. Check your connection and try '
                    'again.',
            onRetry: _reload,
          );
        }
        final groups = groupAgenda(boardViewModel.agendaItems, today: today);
        return RefreshIndicator(
          onRefresh: _reload,
          child: groups.isEmpty
              ? _empty()
              : ListView(
                  padding: const EdgeInsets.only(bottom: 16),
                  children: [
                    for (final group in groups)
                      ..._groupRows(context, group, today),
                  ],
                ),
        );
      },
    );
  }

  /// Scrollable so pull to refresh still works when it's empty.
  Widget _empty() => ListView(
    children: const [
      Padding(
        padding: EdgeInsets.all(24),
        child: Text(
          'Nothing is dated. Undated items are in the Backlog.',
          textAlign: TextAlign.center,
        ),
      ),
    ],
  );

  List<Widget> _groupRows(
    BuildContext context,
    AgendaGroup group,
    DateTime today,
  ) {
    final theme = Theme.of(context);
    return [
      Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
        child: Text(
          agendaHeading(group.day, today: today),
          style: theme.textTheme.titleSmall?.copyWith(
            color: group.isOverdue ? theme.colorScheme.error : null,
          ),
        ),
      ),
      for (final item in group.items)
        AgendaItemTile(
          item: item,
          details: _detailsOf(item, group, today),
          statuses: boardViewModel.statusesOfSpace(item.spaceId),
          onOpen: () => onItemOpened(item.id),
          onStatusChanged: (statusId) =>
              unawaited(boardViewModel.changeStatus(item.id, statusId)),
        ),
      const Divider(height: 1),
    ];
  }

  List<String> _detailsOf(WorkItem item, AgendaGroup group, DateTime today) => [
    ?agendaRange(item.startDate, item.endDate, day: group.day, today: today),
    if (boardViewModel.spansSpaces) ?_spaceNameOf(item.spaceId),
  ];

  String? _spaceNameOf(String spaceId) => boardViewModel.shownSpaces
      .where((space) => space.id == spaceId)
      .firstOrNull
      ?.name;
}
