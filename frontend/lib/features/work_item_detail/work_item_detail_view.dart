import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/board/widgets/date_format.dart';
import 'package:weaver/features/board/widgets/schedule_dialog.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

class WorkItemDetailView extends StatefulWidget {
  const WorkItemDetailView({required this.workItemId, super.key});

  final String workItemId;

  @override
  State<WorkItemDetailView> createState() => _WorkItemDetailViewState();
}

class _WorkItemDetailViewState extends State<WorkItemDetailView> {
  final WorkItemDetailViewModel _viewModel = getIt<WorkItemDetailViewModel>();
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  bool _fieldsInitialized = false;
  String? _selectedLayerId;
  WorkItemPriority _selectedPriority = WorkItemPriority.medium;
  String? _selectedAssigneeId;

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load(widget.workItemId));
    _viewModel.addListener(_onViewModelChanged);
  }

  void _onViewModelChanged() {
    final item = _viewModel.item;
    if (item != null && !_fieldsInitialized) {
      _titleController.text = item.title;
      _descriptionController.text = item.description ?? '';
      _selectedLayerId = item.layerId;
      _selectedPriority = item.priority;
      _selectedAssigneeId = item.assignedToUserId;
      _fieldsInitialized = true;
    }
    setState(() {});
  }

  @override
  void dispose() {
    _viewModel
      ..removeListener(_onViewModelChanged)
      ..dispose();
    _titleController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Work Item Details')),
      body: Builder(
        builder: (context) {
          if (_viewModel.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }
          final loadError = _viewModel.loadError;
          if (loadError != null) {
            return Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(loadError)));
          }
          return _buildForm(context);
        },
      ),
    );
  }

  Widget _buildForm(BuildContext context) {
    final item = _viewModel.item!;
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _titleController,
              decoration: const InputDecoration(labelText: 'Title'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _descriptionController,
              decoration: const InputDecoration(labelText: 'Description'),
              maxLines: 4,
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String?>(
              initialValue: _selectedLayerId,
              decoration: const InputDecoration(labelText: 'Layer'),
              items: [
                const DropdownMenuItem(value: null, child: Text('None')),
                for (final layer in _viewModel.layers)
                  DropdownMenuItem(value: layer.id, child: Text(layer.name)),
              ],
              onChanged: (value) => setState(() => _selectedLayerId = value),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<WorkItemPriority>(
              initialValue: _selectedPriority,
              decoration: const InputDecoration(labelText: 'Priority'),
              items: [
                for (final priority in WorkItemPriority.values)
                  DropdownMenuItem(value: priority, child: Text(priority.label)),
              ],
              onChanged: (value) => setState(() => _selectedPriority = value ?? WorkItemPriority.medium),
            ),
            const SizedBox(height: 16),
            if (_viewModel.saveError != null) ...[
              Text(_viewModel.saveError!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
              const SizedBox(height: 8),
            ],
            FilledButton(
              onPressed: _viewModel.isSaving ? null : () => unawaited(_saveDetails()),
              child: Text(_viewModel.isSaving ? 'Saving…' : 'Save'),
            ),
            const Divider(height: 32),
            Text('Status', style: Theme.of(context).textTheme.labelLarge),
            Text(_viewModel.statusName ?? item.statusId),
            const SizedBox(height: 16),
            Text('Assigned to', style: Theme.of(context).textTheme.labelLarge),
            DropdownButtonFormField<String?>(
              initialValue: _selectedAssigneeId,
              items: [
                const DropdownMenuItem(value: null, child: Text('Unassigned')),
                for (final user in _viewModel.users)
                  DropdownMenuItem(value: user.id, child: Text(user.username)),
              ],
              onChanged: (value) => unawaited(_saveAssignee(value)),
            ),
            const SizedBox(height: 16),
            Text('Schedule', style: Theme.of(context).textTheme.labelLarge),
            Row(
              children: [
                Expanded(child: Text(_scheduleLabel(item.startDate, item.endDate))),
                TextButton(
                  onPressed: () => unawaited(_editSchedule(item.startDate, item.endDate)),
                  child: const Text('Edit'),
                ),
              ],
            ),
            const SizedBox(height: 16),
            Text('Created ${formatDate(item.createdAtUtc)} · Updated ${formatDate(item.updatedAtUtc)}',
                style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }

  String _scheduleLabel(DateTime? start, DateTime? end) {
    if (start != null && end != null) return '${formatDate(start)} → ${formatDate(end)}';
    if (start != null) return 'From ${formatDate(start)}';
    if (end != null) return 'Until ${formatDate(end)}';
    return 'Not scheduled';
  }

  Future<void> _saveDetails() async {
    await _viewModel.saveDetails(
      title: _titleController.text,
      description: _descriptionController.text.isEmpty ? null : _descriptionController.text,
      layerId: _selectedLayerId,
      priority: _selectedPriority,
    );
  }

  Future<void> _saveAssignee(String? userId) async {
    final previous = _selectedAssigneeId;
    setState(() => _selectedAssigneeId = userId);
    final ok = await _viewModel.saveAssignee(userId);
    if (!ok && mounted) setState(() => _selectedAssigneeId = previous);
  }

  Future<void> _editSchedule(DateTime? start, DateTime? end) async {
    final result = await showScheduleDialog(context, initialStart: start, initialEnd: end);
    if (result == null) return;
    await _viewModel.saveSchedule(result.startDate, result.endDate);
  }
}
