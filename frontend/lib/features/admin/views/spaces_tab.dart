import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/views/space_history_page.dart';
import 'package:weaver/features/admin/widgets/admin_add_action.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/features/admin/widgets/space_tile.dart';
import 'package:weaver/shared/models/space.dart';

/// The Spaces work items live in, each with how long its Done items stay
/// on the board and its history. Tapping one renames it.
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
        addAction: AdminAddAction(
          'Add Space',
          onPressed: () => unawaited(
            showNameDialog(
              context,
              title: 'Add Space',
              label: 'Name',
              submitLabel: 'Add',
              onSave: viewModel.create,
            ),
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

  Widget _tile(BuildContext context, Space space) => SpaceTile(
    space: space,
    onRename: (name) => viewModel.rename(space.id, name),
    onSetDoneHidingPeriod: (days) =>
        viewModel.setDoneHidingPeriod(space.id, days),
    onShowHistory: () => unawaited(showSpaceHistoryPage(context, space)),
  );
}
