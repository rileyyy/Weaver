import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/delete_lookup_dialog.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/features/admin/widgets/reorderable_lookup_list.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';

/// The Priorities or Layers tab.
class LookupTab extends StatelessWidget {
  const LookupTab({super.key, required this.viewModel});

  final LookupAdminViewModel viewModel;

  LookupKind get _kind => viewModel.kind;

  String get _description => switch (_kind) {
    LookupKind.priorities =>
      'Priorities, from least to most urgent. Drag to reorder.',
    LookupKind.layers =>
      'Layers label what kind of thing a work item is. Drag to reorder.',
  };

  @override
  Widget build(BuildContext context) {
    final singular = _kind.singular;
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description: _description,
        addLabel: 'Add $singular',
        onAdd: () => unawaited(
          showNameDialog(
            context,
            title: 'Add $singular',
            label: 'Name',
            submitLabel: 'Add',
            onSave: viewModel.create,
          ),
        ),
        child: ReorderableLookupList<LookupValue>(
          items: viewModel.items,
          idOf: (value) => value.id,
          titleOf: (value) => value.name,
          onReorder: (oldIndex, newIndex) {
            final messenger = ScaffoldMessenger.of(context);
            unawaited(
              viewModel
                  .move(oldIndex, newIndex)
                  .then((error) => showAdminError(messenger, error)),
            );
          },
          onEdit: (value) => unawaited(
            showNameDialog(
              context,
              title: 'Rename $singular',
              label: 'Name',
              submitLabel: 'Save',
              initialName: value.name,
              onSave: (name) => viewModel.rename(value.id, name),
            ),
          ),
          onDelete: (value) => unawaited(_delete(context, value)),
        ),
      ),
    );
  }

  Future<void> _delete(BuildContext context, LookupValue value) =>
      showDeleteLookupDialog(
        context,
        kind: _kind.singular,
        name: value.name,
        noneLabel: 'No ${_kind.singular}',
        replacements: [
          for (final other in viewModel.items)
            if (other.id != value.id) (id: other.id, name: other.name),
        ],
        onDelete: (replacementId) =>
            viewModel.delete(value.id, replacementId: replacementId),
      );
}
