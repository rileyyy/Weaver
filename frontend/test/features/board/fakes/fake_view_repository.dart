import 'package:weaver/features/board/data/view_repository.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';

/// Overview's id, as the server gives it.
const overviewId = '00000000-0000-0000-0000-000000000001';

/// Every Space with the header's default filters: the board as it was before
/// Views, so tests that aren't about Views see one Space's board.
const allSpacesView = SavedView(
  id: 'all',
  definition: ViewDefinition(name: 'Everything'),
);

/// [allSpacesView] with Done items shown, so no header filter is set.
const allSpacesShowingDoneView = SavedView(
  id: 'all',
  definition: ViewDefinition(name: 'Everything', applyDoneHiding: false),
);

/// Views held in memory. The default is [allSpacesView] until a test sets
/// [defaultView] or calls [setDefault].
class FakeViewRepository implements ViewRepository {
  FakeViewRepository({
    SavedView defaultView = allSpacesView,
    List<SavedView>? views,
  }) : _default = defaultView,
       views = views ?? [defaultView];

  SavedView _default;
  final List<SavedView> views;
  int _nextId = 1;

  /// Set to make every call fail with it.
  Exception? failure;

  SavedView get defaultView => _default;

  Future<T> _answer<T>(T Function() value) async {
    if (failure case final error?) throw error;
    return value();
  }

  @override
  Future<List<SavedView>> list() => _answer(() => [...views]);

  @override
  Future<SavedView> create(ViewDefinition definition) => _answer(() {
    final view = SavedView(id: 'view-${_nextId++}', definition: definition);
    views.add(view);
    return view;
  });

  @override
  Future<SavedView> update(String id, ViewDefinition definition) => _answer(() {
    final view = SavedView(id: id, definition: definition);
    views[views.indexWhere((v) => v.id == id)] = view;
    return view;
  });

  @override
  Future<void> delete(String id) => _answer(() {
    views.removeWhere((v) => v.id == id);
    if (_default.id == id) _default = views.first;
  });

  @override
  Future<SavedView> loadDefault() => _answer(() => _default);

  @override
  Future<SavedView> setDefault(String id) =>
      _answer(() => _default = views.firstWhere((v) => v.id == id));
}
