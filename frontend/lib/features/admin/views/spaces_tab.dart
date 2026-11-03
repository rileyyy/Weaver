import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/shared/models/space.dart';

/// The Spaces work items live in. Tapping one renames it.
class SpacesTab extends StatelessWidget {
  const SpacesTab({super.key, required this.viewModel});

  final SpaceAdminViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description:
            'Every work item belongs to one Space, such as Home or Software. '
            'Items can only be moved under items in the same Space.',
        addLabel: 'Add Space',
        onAdd: () => unawaited(
          showNameDialog(
            context,
            title: 'Add Space',
            label: 'Name',
            submitLabel: 'Add',
            onSave: viewModel.create,
          ),
        ),
        child: ListView.separated(
          padding: const EdgeInsets.only(bottom: 16),
          itemCount: viewModel.spaces.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) =>
              _tile(context, viewModel.spaces[index]),
        ),
      ),
    );
  }

  Widget _tile(BuildContext context, Space space) => ListTile(
    title: Text(space.name),
    trailing: IconButton(
      tooltip: 'Rename ${space.name}',
      icon: const Icon(Icons.edit_outlined),
      onPressed: () => unawaited(_rename(context, space)),
    ),
    onTap: () => unawaited(_rename(context, space)),
  );

  Future<bool> _rename(BuildContext context, Space space) => showNameDialog(
    context,
    title: 'Rename Space',
    label: 'Name',
    submitLabel: 'Rename',
    initialName: space.name,
    onSave: (name) => viewModel.rename(space.id, name),
  );
}
