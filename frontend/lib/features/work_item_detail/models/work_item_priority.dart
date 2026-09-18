enum WorkItemPriority { low, medium, high, urgent }

WorkItemPriority workItemPriorityFromWire(String value) => switch (value) {
      'Low' => WorkItemPriority.low,
      'High' => WorkItemPriority.high,
      'Urgent' => WorkItemPriority.urgent,
      _ => WorkItemPriority.medium,
    };

extension WorkItemPriorityWire on WorkItemPriority {
  String get label => switch (this) {
        WorkItemPriority.low => 'Low',
        WorkItemPriority.medium => 'Medium',
        WorkItemPriority.high => 'High',
        WorkItemPriority.urgent => 'Urgent',
      };

  String toWire() => label;
}
