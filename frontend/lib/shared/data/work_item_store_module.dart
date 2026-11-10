import 'package:injectable/injectable.dart';
import 'package:weaver/shared/data/blocked_item_refresher.dart';
import 'package:weaver/shared/data/work_item_store.dart';

@module
abstract class WorkItemStoreModule {
  /// The app's one store, with the dependency-conflict refresh attached
  /// here rather than inside it, so the store stays unaware of blocks
  /// links.
  @lazySingleton
  WorkItemStore workItemStore() {
    final store = WorkItemStore();
    BlockedItemRefresher.attachTo(store);
    return store;
  }
}
