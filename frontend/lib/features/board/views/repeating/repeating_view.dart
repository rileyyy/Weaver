import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/repeating_items_view_model.dart';
import 'package:weaver/features/board/views/repeating/edit_recurrence_dialog.dart';
import 'package:weaver/features/board/widgets/load_error_view.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';
import 'package:weaver/shared/recurrence/widgets/confirm_stop_repeating.dart';

/// The Repeating tab: every repeating work item with its schedule, where
/// each can be edited or stopped without opening the item itself.
class RepeatingView extends StatelessWidget {
  const RepeatingView({
    super.key,
    required this.viewModel,
    required this.onItemOpened,
    required this.onSchedulesChanged,
  });

  final RepeatingItemsViewModel viewModel;
  final ValueChanged<String> onItemOpened;

  /// Called after a save or stop succeeds. A save can create items on the
  /// board straight away.
  final VoidCallback onSchedulesChanged;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (viewModel.isLoading) {
          return const Center(child: CircularProgressIndicator());
        }
        if (viewModel.loadError case final error?) {
          return LoadErrorView(message: error, onRetry: viewModel.load);
        }
        final items = viewModel.items;
        if (items.isEmpty) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'No repeating items yet. Open a work item and choose '
                '"Repeat this item" to set one up.',
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        return ListView.separated(
          padding: const EdgeInsets.symmetric(vertical: 8),
          itemCount: items.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) => _RepeatingItemTile(
            recurrence: items[index],
            onOpen: onItemOpened,
            onEdit: (recurrence) => unawaited(_edit(context, recurrence)),
            onStop: (recurrence) => unawaited(_stop(context, recurrence)),
          ),
        );
      },
    );
  }

  Future<void> _edit(
    BuildContext context,
    WorkItemRecurrence recurrence,
  ) async {
    final saved = await showEditRecurrenceDialog(
      context,
      recurrence: recurrence,
      onSave: (draft) => viewModel.save(recurrence.workItemId, draft),
    );
    if (saved) onSchedulesChanged();
  }

  Future<void> _stop(
    BuildContext context,
    WorkItemRecurrence recurrence,
  ) async {
    if (!await confirmStopRepeating(context)) return;
    if (await viewModel.stopRepeating(recurrence.workItemId)) {
      onSchedulesChanged();
      return;
    }
    final error = viewModel.actionError;
    viewModel.clearActionError();
    if (error != null && context.mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(error)));
    }
  }
}

class _RepeatingItemTile extends StatelessWidget {
  const _RepeatingItemTile({
    required this.recurrence,
    required this.onOpen,
    required this.onEdit,
    required this.onStop,
  });

  final WorkItemRecurrence recurrence;
  final ValueChanged<String> onOpen;
  final ValueChanged<WorkItemRecurrence> onEdit;
  final ValueChanged<WorkItemRecurrence> onStop;

  @override
  Widget build(BuildContext context) {
    final end = recurrence.endDate;
    final next = recurrence.nextOccurrence;
    final parent = recurrence.parentTitle;
    final endText = end == null ? 'no end date' : formatDate(end);
    final details = [
      if (parent != null) 'In $parent',
      recurrence.summary,
      '${formatDate(recurrence.startDate)} → $endText',
      next == null ? 'Ended' : 'Next ${formatDate(next)}',
    ].join(' · ');
    return ListTile(
      leading: const Icon(Icons.repeat),
      title: Text(
        '#${recurrence.workItemNumber} ${recurrence.workItemTitle}',
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: Text(details),
      onTap: () => onOpen(recurrence.workItemId),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            icon: const Icon(Icons.edit_calendar_outlined),
            tooltip: 'Edit schedule',
            onPressed: () => onEdit(recurrence),
          ),
          IconButton(
            icon: const Icon(Icons.stop_circle_outlined),
            tooltip: 'Stop repeating',
            onPressed: () => onStop(recurrence),
          ),
        ],
      ),
    );
  }
}
