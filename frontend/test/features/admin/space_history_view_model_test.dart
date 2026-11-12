import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/space_history_view_model.dart';
import 'package:weaver/shared/models/space.dart';

import 'fakes/fake_space_admin_repository.dart';
import 'fakes/space_history_entries.dart';

void main() {
  late FakeSpaceAdminRepository repository;
  late SpaceHistoryViewModel viewModel;

  setUp(() {
    repository = FakeSpaceAdminRepository(
      const [Space(id: 'home', name: 'Home')],
      history: {
        'home': [
          spaceHistoryEntry(2, 'name', oldValue: 'Home', newValue: 'House'),
          spaceHistoryEntry(1, 'created', newValue: 'Home'),
        ],
      },
    );
    viewModel = SpaceHistoryViewModel(repository, 'home');
  });

  tearDown(() => viewModel.dispose());

  test("loads the Space's history in the order the server gives", () async {
    await viewModel.load();

    expect(repository.calls, ['history home']);
    expect([for (final e in viewModel.entries) e.id], [2, 1]);
    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
  });

  test('a failed load reports an error, and a retry clears it', () async {
    repository.error = const ApiException('down', statusCode: 503);
    await viewModel.load();

    expect(viewModel.loadError, isNotNull);

    repository.error = null;
    await viewModel.load();

    expect(viewModel.loadError, isNull);
    expect(viewModel.entries, hasLength(2));
  });
}
