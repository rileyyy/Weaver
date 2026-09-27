import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart';
import 'package:weaver/shared/recurrence/widgets/recurrence_editor.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The detail dialog's Repeat section: set up, edit or stop repeating this
/// item, or, for an item a repetition created, say where it came from.
class RepeatSection extends StatelessWidget {
  const RepeatSection({
    super.key,
    required this.viewModel,
    required this.itemStartDate,
    required this.occurrenceDate,
    required this.onOpenSource,
    required this.onStopRepeating,
  });

  final RepeatSettingsViewModel viewModel;
  final DateTime? itemStartDate;
  final DateTime? occurrenceDate;
  final ValueChanged<String> onOpenSource;
  final VoidCallback onStopRepeating;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [const FieldLabel('Repeat'), _body(context)],
    );
  }

  Widget _body(BuildContext context) {
    final smallText = Theme.of(context).textTheme.bodySmall;
    if (viewModel.isLoading) {
      return const LinearProgressIndicator();
    }
    if (viewModel.loadError case final error?) {
      return Text(error, style: smallText);
    }
    if (viewModel.isOccurrence) return _occurrenceNote(context);

    final draft = viewModel.draft;
    if (draft == null) {
      return Row(
        children: [
          Expanded(child: Text('Does not repeat', style: smallText)),
          TextButton.icon(
            icon: const Icon(Icons.repeat),
            label: const Text('Repeat this item'),
            onPressed: () =>
                viewModel.startRepeating(itemStartDate: itemStartDate),
          ),
        ],
      );
    }

    final saved = viewModel.saved;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'A copy of this item and its sub-items is added to the same '
          'parent a week before each date.',
          style: smallText,
        ),
        const SizedBox(height: 8),
        RecurrenceEditor(
          draft: draft,
          enabled: !viewModel.isSaving,
          onChanged: viewModel.updateDraft,
        ),
        if (draft.validationError case final error?)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              error,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        if (viewModel.saveError case final error?)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              error,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ),
        if (saved != null && !viewModel.isDirty)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              saved.nextOccurrence == null
                  ? 'This repetition has ended.'
                  : 'Next: ${formatDate(saved.nextOccurrence!)}',
              style: smallText,
            ),
          ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (saved != null)
              TextButton(
                onPressed: viewModel.isSaving ? null : onStopRepeating,
                child: const Text('Stop repeating'),
              ),
            if (viewModel.isDirty)
              TextButton(
                onPressed: viewModel.isSaving ? null : viewModel.discardChanges,
                child: Text(saved == null ? 'Cancel' : 'Discard changes'),
              ),
            FilledButton.tonal(
              onPressed: viewModel.canSave
                  ? () => unawaited(viewModel.save())
                  : null,
              child: Text(viewModel.isSaving ? 'Saving…' : 'Save repeat'),
            ),
          ],
        ),
      ],
    );
  }

  Widget _occurrenceNote(BuildContext context) {
    final smallText = Theme.of(context).textTheme.bodySmall;
    final source = viewModel.source;
    final date = occurrenceDate;
    final forDate = date == null ? '' : ' for ${formatDate(date)}';
    if (source == null) {
      return Text(
        'Created by a repeating item$forDate. That item no longer repeats.',
        style: smallText,
      );
    }
    return Row(
      children: [
        Expanded(
          child: Text(
            'Created$forDate by #${source.workItemNumber} '
            '${source.workItemTitle} · ${source.summary}',
            style: smallText,
          ),
        ),
        TextButton(
          onPressed: () => onOpenSource(source.workItemId),
          child: const Text('Open'),
        ),
      ],
    );
  }
}
