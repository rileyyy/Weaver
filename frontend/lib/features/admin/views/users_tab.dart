import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/user_action.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/confirm_admin_action.dart';
import 'package:weaver/features/admin/widgets/create_user_dialog.dart';
import 'package:weaver/features/admin/widgets/name_dialog.dart';
import 'package:weaver/features/admin/widgets/reset_password_dialog.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/features/admin/widgets/user_tile.dart';

class UsersTab extends StatelessWidget {
  const UsersTab({super.key, required this.viewModel});

  final UserAdminViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description:
            'Everyone who can sign in. Deactivated accounts keep their '
            'comments but can’t sign in or be assigned work.',
        addLabel: 'Add user',
        onAdd: () => unawaited(
          showCreateUserDialog(context, onCreate: viewModel.create),
        ),
        child: ListView.separated(
          padding: const EdgeInsets.only(bottom: 16),
          itemCount: viewModel.users.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) {
            final user = viewModel.users[index];
            return UserTile(
              user: user,
              isCurrentUser: viewModel.isCurrentUser(user),
              onAction: (action) => unawaited(_run(context, user, action)),
            );
          },
        ),
      ),
    );
  }

  Future<void> _run(
    BuildContext context,
    AdminUser user,
    UserAction action,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    switch (action) {
      case UserAction.rename:
        await showNameDialog(
          context,
          title: 'Rename ${user.username}',
          label: 'Username',
          submitLabel: 'Save',
          initialName: user.username,
          onSave: (username) => viewModel.rename(user.id, username),
        );
      case UserAction.resetPassword:
        await showResetPasswordDialog(
          context,
          username: user.username,
          onReset: (password) => viewModel.resetPassword(user.id, password),
        );
      case UserAction.unlock:
        showAdminError(messenger, await viewModel.unlock(user.id));
      case UserAction.grantAdmin:
        showAdminError(
          messenger,
          await viewModel.setAdmin(user.id, isAdmin: true),
        );
      case UserAction.revokeAdmin:
        showAdminError(
          messenger,
          await viewModel.setAdmin(user.id, isAdmin: false),
        );
      case UserAction.reactivate:
        showAdminError(
          messenger,
          await viewModel.setActive(user.id, isActive: true),
        );
      case UserAction.deactivate:
        final confirmed = await confirmAdminAction(
          context,
          title: 'Deactivate ${user.username}?',
          message:
              'They will be signed out, can’t sign in, and will be '
              'unassigned from their work items. Their comments stay. You '
              'can reactivate the account later.',
          confirmLabel: 'Deactivate',
          isDestructive: true,
        );
        if (!confirmed || !context.mounted) return;
        showAdminError(
          messenger,
          await viewModel.setActive(user.id, isActive: false),
        );
      case UserAction.delete:
        final confirmed = await confirmAdminAction(
          context,
          title: 'Delete ${user.username}?',
          message:
              'The account is removed for good and unassigned from its work '
              'items. Accounts that have written comments can only be '
              'deactivated.',
          confirmLabel: 'Delete',
          isDestructive: true,
        );
        if (!confirmed || !context.mounted) return;
        showAdminError(messenger, await viewModel.delete(user.id));
    }
  }
}
