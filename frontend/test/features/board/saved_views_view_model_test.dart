import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/saved_views_view_model.dart';

import 'fakes/fake_view_repository.dart';

const _overview = SavedView(
  id: overviewId,
  builtIn: true,
  definition: ViewDefinition(name: 'Overview', scheduledOrInProgressOnly: true),
);
const _village = SavedView(
  id: 'village',
  definition: ViewDefinition(name: 'Village', spaceIds: {'v'}),
);

void main() {
  late FakeViewRepository repository;
  late SavedViewsViewModel viewModel;

  setUp(() async {
    repository = FakeViewRepository(
      defaultView: _overview,
      views: [_overview, _village],
    );
    viewModel = SavedViewsViewModel(repository);
    await viewModel.load();
    viewModel.knowDefault(overviewId);
  });

  tearDown(() => viewModel.dispose());

  test('loads every View, Overview first', () {
    expect(viewModel.views.map((v) => v.name), ['Overview', 'Village']);
    expect(viewModel.overview?.id, overviewId);
  });

  test('takes the default from the board rather than asking again', () {
    viewModel.knowDefault('village');

    expect(viewModel.defaultViewId, 'village');
  });

  test('a saved View is listed by name after Overview', () async {
    final saved = await viewModel.create(
      const ViewDefinition(name: 'Allotment'),
    );

    expect(saved?.name, 'Allotment');
    expect(viewModel.views.map((v) => v.name), [
      'Overview',
      'Allotment',
      'Village',
    ]);
  });

  test('a name already taken says so and saves nothing', () async {
    repository.failure = const ApiException(
      'Failed',
      statusCode: 409,
      detail: "A View named 'Village' already exists.",
    );

    final saved = await viewModel.create(const ViewDefinition(name: 'Village'));

    expect(saved, isNull);
    expect(viewModel.error, "A View named 'Village' already exists.");
    expect(viewModel.views, hasLength(2));
  });

  test('renaming keeps the rest of the definition', () async {
    final renamed = await viewModel.rename(_village, 'The village');

    expect(renamed?.name, 'The village');
    expect(renamed?.definition.spaceIds, {'v'});
    expect(viewModel.views.last.name, 'The village');
  });

  test('deleting the default View makes Overview the default', () async {
    await viewModel.setDefault(_village);

    final deleted = await viewModel.delete(_village);

    expect(deleted, isTrue);
    expect(viewModel.views.map((v) => v.name), ['Overview']);
    expect(viewModel.defaultViewId, overviewId);
  });

  test('setting the default is remembered', () async {
    await viewModel.setDefault(_village);

    expect(viewModel.defaultViewId, 'village');
    expect(repository.defaultView.id, 'village');
  });

  test('a failed load says so', () async {
    repository.failure = const ApiException('Down');

    await viewModel.load();

    expect(viewModel.error, isNotNull);
  });
}
