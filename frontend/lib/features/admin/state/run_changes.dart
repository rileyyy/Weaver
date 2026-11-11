import 'package:weaver/features/admin/models/run_changed_item.dart';

/// Where loading one run's changes has got to: under way, failed with
/// [error], or done with [items].
class RunChanges {
  const RunChanges.loading() : isLoading = true, error = null, items = const [];

  const RunChanges.failed(String this.error)
    : isLoading = false,
      items = const [];

  const RunChanges.loaded(this.items) : isLoading = false, error = null;

  final bool isLoading;
  final String? error;
  final List<RunChangedItem> items;
}
