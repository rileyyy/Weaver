import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/delete_lookup_dialog.dart';
import 'package:weaver/features/admin/widgets/reorderable_lookup_list.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/features/admin/widgets/status_dialog.dart';
import 'package:weaver/shared/models/hex_color.dart';

class StatusesTab extends StatelessWidget {
  const StatusesTab({super.key, required this.viewModel});

  final StatusAdminViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description:
            'Statuses are the board’s columns, shown in this order. '
            'Drag to reorder.',
        addLabel: 'Add status',
        onAdd: () =>
            unawaited(showStatusDialog(context, onSave: viewModel.create)),
        child: ReorderableLookupList<AdminStatus>(
          items: viewModel.items,
          idOf: (status) => status.id,
          titleOf: (status) => status.name,
          subtitleOf: (status) => status.category.label,
          swatchOf: (status) => parseHexColor(status.color),
          deleteDisabledReason: viewModel.canDelete
              ? null
              : 'At least one status must remain',
          onReorder: (oldIndex, newIndex) {
            final messenger = ScaffoldMessenger.of(context);
            unawaited(
              viewModel
                  .move(oldIndex, newIndex)
                  .then((error) => showAdminError(messenger, error)),
            );
          },
          onEdit: (status) => unawaited(
            showStatusDialog(
              context,
              initial: status,
              onSave: (details) => viewModel.update(status.id, details),
            ),
          ),
          onDelete: (status) => unawaited(_delete(context, status)),
        ),
      ),
    );
  }

  Future<void> _delete(BuildContext context, AdminStatus status) =>
      showDeleteLookupDialog(
        context,
        kind: 'status',
        name: status.name,
        replacements: [
          for (final other in viewModel.items)
            if (other.id != status.id) (id: other.id, name: other.name),
        ],
        onDelete: (replacementId) =>
            viewModel.delete(status.id, replacementId: replacementId!),
      );
}
