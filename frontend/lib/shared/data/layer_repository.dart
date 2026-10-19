import 'package:weaver/shared/models/work_item_layer.dart';

/// The configured work-item layers, shared by every feature.
abstract class LayerRepository {
  Future<List<WorkItemLayer>> loadLayers();

  /// Drops the cached list, so the next [loadLayers] fetches it again.
  /// Called after an admin changes the layers.
  void invalidate();
}
