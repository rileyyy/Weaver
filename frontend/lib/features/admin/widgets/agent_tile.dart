import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/agent_account.dart';
import 'package:weaver/features/admin/models/agent_action.dart';
import 'package:weaver/shared/widgets/user_state_badge.dart';

/// One agent account; tapping it opens its keys.
class AgentTile extends StatelessWidget {
  const AgentTile({
    super.key,
    required this.agent,
    required this.onTap,
    required this.onAction,
  });

  final AgentAccount agent;
  final VoidCallback onTap;
  final ValueChanged<AgentAction> onAction;

  @override
  Widget build(BuildContext context) {
    final description = agent.description;
    final handle = agent.handle;
    return ListTile(
      onTap: onTap,
      leading: const CircleAvatar(child: Icon(Icons.smart_toy_outlined)),
      title: Text(
        agent.name,
        overflow: TextOverflow.ellipsis,
        style: agent.isActive
            ? null
            : TextStyle(color: Theme.of(context).disabledColor),
      ),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (handle != null) Text('@$handle'),
          if (description != null)
            Text(description, maxLines: 2, overflow: TextOverflow.ellipsis),
          if (!agent.isActive)
            const Padding(
              padding: EdgeInsets.only(top: 4),
              child: UserStateBadge(label: 'Deactivated', isWarning: true),
            ),
        ],
      ),
      trailing: PopupMenuButton<AgentAction>(
        tooltip: 'Actions for ${agent.name}',
        onSelected: onAction,
        itemBuilder: (context) => [
          for (final action in AgentAction.availableFor(agent))
            PopupMenuItem(value: action, child: Text(action.label)),
        ],
      ),
    );
  }
}
