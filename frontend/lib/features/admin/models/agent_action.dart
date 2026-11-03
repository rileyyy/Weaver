import 'package:weaver/features/admin/models/agent_account.dart';

/// What the Agents tab's row menu offers.
enum AgentAction {
  manageKeys('Manage keys'),
  deactivate('Deactivate'),
  reactivate('Reactivate');

  const AgentAction(this.label);

  final String label;

  static List<AgentAction> availableFor(AgentAccount agent) => [
    manageKeys,
    agent.isActive ? deactivate : reactivate,
  ];
}
