import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/work_item_picker/work_item_pick.dart';
import 'package:weaver/shared/work_item_picker/work_item_picker_view_model.dart';

/// Lets the user find a work item by title or `#Number` with [search] and
/// pick it. With [noneLabel], an extra option picks no item. Completes with
/// null when cancelled.
Future<WorkItemPick?> showWorkItemPicker(
  BuildContext context, {
  required String title,
  required WorkItemSearch search,
  String? noneLabel,
}) => showDialog<WorkItemPick>(
  context: context,
  builder: (_) => WorkItemPickerDialog(
    title: title,
    viewModel: WorkItemPickerViewModel(search),
    noneLabel: noneLabel,
  ),
);

class WorkItemPickerDialog extends StatefulWidget {
  const WorkItemPickerDialog({
    super.key,
    required this.title,
    required this.viewModel,
    this.noneLabel,
  });

  final String title;

  /// Disposed with the dialog.
  final WorkItemPickerViewModel viewModel;
  final String? noneLabel;

  @override
  State<WorkItemPickerDialog> createState() => _WorkItemPickerDialogState();
}

class _WorkItemPickerDialogState extends State<WorkItemPickerDialog> {
  // Long enough that typing a word sends one search, not one per letter.
  static const Duration _typingPause = Duration(milliseconds: 300);

  late final WorkItemPickerViewModel _viewModel = widget.viewModel;
  Timer? _pendingSearch;

  @override
  void initState() {
    super.initState();
    _viewModel.addListener(_onChanged);
    unawaited(_viewModel.search(''));
  }

  @override
  void dispose() {
    _pendingSearch?.cancel();
    _viewModel
      ..removeListener(_onChanged)
      ..dispose();
    super.dispose();
  }

  void _onChanged() => setState(() {});

  void _onTextChanged(String text) {
    _pendingSearch?.cancel();
    _pendingSearch = Timer(
      _typingPause,
      () => unawaited(_viewModel.search(text)),
    );
  }

  void _pick(WorkItem? item) => Navigator.of(context).pop(WorkItemPick(item));

  @override
  Widget build(BuildContext context) {
    final noneLabel = widget.noneLabel;
    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: 420,
        height: 400,
        child: Column(
          children: [
            TextField(
              autofocus: true,
              decoration: const InputDecoration(
                hintText: 'Search by title or #number',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: _onTextChanged,
            ),
            const SizedBox(height: 8),
            if (_viewModel.isSearching) const LinearProgressIndicator(),
            if (noneLabel != null)
              ListTile(
                leading: const Icon(Icons.block),
                title: Text(noneLabel),
                onTap: () => _pick(null),
              ),
            Expanded(child: _results(context)),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
      ],
    );
  }

  Widget _results(BuildContext context) {
    if (_viewModel.error case final error?) {
      return Center(child: Text(error, textAlign: TextAlign.center));
    }
    final results = _viewModel.results;
    if (results.isEmpty && !_viewModel.isSearching) {
      return const Center(child: Text('No matching work items'));
    }
    return ListView(
      children: [
        for (final item in results)
          ListTile(
            title: Text(
              '#${item.number} ${item.title}',
              overflow: TextOverflow.ellipsis,
            ),
            onTap: () => _pick(item),
          ),
      ],
    );
  }
}
