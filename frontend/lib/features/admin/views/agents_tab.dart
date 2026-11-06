import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/agent_admin_view_model.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/agent_action.dart';
import 'package:weaver/features/admin/views/agent_keys_page.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/agent_tile.dart';
import 'package:weaver/features/admin/widgets/create_agent_dialog.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/shared/widgets/confirm_action.dart';

/// Agent accounts, apart from the people on the Users tab: they have no
/// password and sign in with API keys.
class AgentsTab extends StatelessWidget {
  const AgentsTab({super.key, required this.viewModel});

  final AgentAdminViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) => AdminListFrame(
        isLoading: viewModel.isLoading,
        loadError: viewModel.loadError,
        onRetry: viewModel.load,
        description:
            'Accounts for AI agents, one per agent runtime. They sign in '
            'with API keys instead of a password.',
        addLabel: 'Add agent',
        onAdd: () => unawaited(
          showCreateAgentDialog(context, onCreate: viewModel.create),
        ),
        child: ListView.separated(
          padding: const EdgeInsets.only(bottom: 16),
          itemCount: viewModel.agents.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, index) {
            final agent = viewModel.agents[index];
            return AgentTile(
              agent: agent,
              onTap: () => unawaited(showAgentKeysPage(context, agent)),
              onAction: (action) => unawaited(_run(context, agent, action)),
            );
          },
        ),
      ),
    );
  }

  Future<void> _run(
    BuildContext context,
    AgentAccount agent,
    AgentAction action,
  ) async {
    final messenger = ScaffoldMessenger.of(context);
    switch (action) {
      case AgentAction.manageKeys:
        await showAgentKeysPage(context, agent);
      case AgentAction.reactivate:
        showAdminError(
          messenger,
          await viewModel.setActive(agent.id, isActive: true),
        );
      case AgentAction.deactivate:
        final confirmed = await confirmAction(
          context,
          title: 'Deactivate ${agent.name}?',
          message:
              'It’s signed out, its keys stop working and it’s unassigned '
              'from its work items. Deactivating doesn’t revoke its keys: '
              'they work again if you reactivate it. Revoke them on its '
              'keys page to end them for good.',
          confirmLabel: 'Deactivate',
          isDestructive: true,
        );
        if (!confirmed || !context.mounted) return;
        showAdminError(
          messenger,
          await viewModel.setActive(agent.id, isActive: false),
        );
    }
  }
}
