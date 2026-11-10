import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/backlog_view_model.dart';
import 'package:weaver/features/board/models/backlog_group.dart';
import 'package:weaver/features/board/views/backlog/widgets/backlog_item_tile.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/widgets/load_error_view.dart';

/// A parameter so tests can answer without driving the Material picker.
typedef DateRangeChooser =
    Future<DateTimeRange?> Function(BuildContext context, WorkItem item);

/// The Backlog tab: the shown Spaces' undated, not-Done items, grouped
/// under their parents, each of which can be given dates.
class BacklogView extends StatelessWidget {
  const BacklogView({
    super.key,
    required this.viewModel,
    required this.spaces,
    required this.onItemOpened,
    this.chooseDates = pickDateRange,
  });

  final BacklogViewModel viewModel;

  /// Empty while no Space is shown.
  final List<Space> spaces;
  final void Function(String workItemId) onItemOpened;
  final DateRangeChooser chooseDates;

  static Future<DateTimeRange?> pickDateRange(
    BuildContext context,
    WorkItem item,
  ) => showDateRangePicker(
    context: context,
    firstDate: DateTime(2000),
    lastDate: DateTime(2100),
    helpText: 'Dates for "${item.title}"',
  );

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        if (viewModel.loadError case final error?) {
          return LoadErrorView(
            message: error,
            onRetry: () async {
              if (spaces.isNotEmpty) await viewModel.load(spaces);
            },
          );
        }
        final groups = viewModel.groups;
        if (groups.isEmpty) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'The Backlog is empty: everything not Done has dates.',
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        return ListView(
          padding: const EdgeInsets.symmetric(vertical: 8),
          children: [for (final group in groups) ..._groupRows(context, group)],
        );
      },
    );
  }

  List<Widget> _groupRows(BuildContext context, BacklogGroup group) {
    final parent = group.parent;
    return [
      ListTile(
        dense: true,
        title: Text(
          switch ((parent, group.space)) {
            (final parent?, _) => '#${parent.number} ${parent.title}',
            (null, final space?) => '${space.name}: top level',
            (null, null) => 'Top level',
          },
          style: Theme.of(context).textTheme.titleSmall,
          overflow: TextOverflow.ellipsis,
        ),
        onTap: parent == null ? null : () => onItemOpened(parent.id),
      ),
      for (final item in group.items)
        BacklogItemTile(
          item: item,
          onOpen: () => onItemOpened(item.id),
          onSetDates: () => unawaited(_setDates(context, item)),
        ),
      const Divider(height: 1),
    ];
  }

  Future<void> _setDates(BuildContext context, WorkItem item) async {
    final range = await chooseDates(context, item);
    if (range == null) return;
    if (await viewModel.setDates(item.id, range.start, range.end)) return;
    if (!context.mounted) return;
    final error = viewModel.actionError;
    viewModel.clearActionError();
    if (error == null) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
  }
}
