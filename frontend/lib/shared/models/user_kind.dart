enum UserKind { human, agent }

UserKind userKindFromWire(String value) => switch (value) {
  'Agent' => UserKind.agent,
  _ => UserKind.human,
};
