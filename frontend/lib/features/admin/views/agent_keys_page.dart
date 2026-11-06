import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/agent_keys_view_model.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/api_key.dart';
import 'package:weaver/features/admin/widgets/admin_list_frame.dart';
import 'package:weaver/features/admin/widgets/api_key_tile.dart';
import 'package:weaver/features/admin/widgets/issue_key_dialog.dart';
import 'package:weaver/features/admin/widgets/show_admin_error.dart';
import 'package:weaver/shared/widgets/confirm_action.dart';

/// Opens [agent]'s keys; completes when the page is closed.
Future<void> showAgentKeysPage(BuildContext context, AgentAccount agent) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => AgentKeysPage(
          agent: agent,
          viewModel: getIt<AgentKeysViewModel>(param1: agent.id),
        ),
      ),
    );

/// One agent's API keys: issue, see when each was last used, revoke. Owns
/// [viewModel] and disposes it when closed.
class AgentKeysPage extends StatefulWidget {
  const AgentKeysPage({
    super.key,
    required this.agent,
    required this.viewModel,
  });

  final AgentAccount agent;
  final AgentKeysViewModel viewModel;

  @override
  State<AgentKeysPage> createState() => _AgentKeysPageState();
}

class _AgentKeysPageState extends State<AgentKeysPage> {
  AgentKeysViewModel get _viewModel => widget.viewModel;

  @override
  void initState() {
    super.initState();
    unawaited(_viewModel.load());
  }

  @override
  void dispose() {
    _viewModel.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Keys for ${widget.agent.name}')),
      body: ListenableBuilder(
        listenable: _viewModel,
        builder: (context, _) => AdminListFrame(
          isLoading: _viewModel.isLoading,
          loadError: _viewModel.loadError,
          onRetry: _viewModel.load,
          description:
              'The keys this agent signs in with. Each key is shown once, '
              'when it’s issued. Revoked keys stay listed.',
          addLabel: 'Issue key',
          onAdd: () => unawaited(
            showIssueKeyDialog(
              context,
              agentName: widget.agent.name,
              onIssue: _viewModel.issueKey,
            ),
          ),
          child: ListView.separated(
            padding: const EdgeInsets.only(bottom: 16),
            itemCount: _viewModel.keys.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, index) {
              final key = _viewModel.keys[index];
              return ApiKeyTile(
                apiKey: key,
                onRevoke: () => unawaited(_revoke(context, key)),
              );
            },
          ),
        ),
      ),
    );
  }

  Future<void> _revoke(BuildContext context, ApiKey key) async {
    final messenger = ScaffoldMessenger.of(context);
    final confirmed = await confirmAction(
      context,
      title: 'Revoke ${key.label}?',
      message:
          'Anything using this key is refused from now on. This can’t be '
          'undone; issue a new key if it’s needed again.',
      confirmLabel: 'Revoke',
      isDestructive: true,
    );
    if (!confirmed || !context.mounted) return;
    showAdminError(messenger, await _viewModel.revokeKey(key.id));
  }
}
