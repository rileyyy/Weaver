// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format width=80

// **************************************************************************
// InjectableConfigGenerator
// **************************************************************************

// ignore_for_file: type=lint
// coverage:ignore-file

// ignore_for_file: no_leading_underscores_for_library_prefixes
import 'package:get_it/get_it.dart' as _i174;
import 'package:http/http.dart' as _i519;
import 'package:injectable/injectable.dart' as _i526;
import 'package:weaver/core/di/network_module.dart' as _i561;
import 'package:weaver/features/board/board_view_model.dart' as _i314;
import 'package:weaver/features/board/data/api_board_repository.dart' as _i436;
import 'package:weaver/features/board/data/board_repository.dart' as _i522;

extension GetItInjectableX on _i174.GetIt {
  // initializes the registration of main-scope dependencies inside of GetIt
  _i174.GetIt init({
    String? environment,
    _i526.EnvironmentFilter? environmentFilter,
  }) {
    final gh = _i526.GetItHelper(this, environment, environmentFilter);
    final networkModule = _$NetworkModule();
    gh.lazySingleton<_i519.Client>(() => networkModule.httpClient);
    gh.factory<String>(
      () => networkModule.apiBaseUrl,
      instanceName: 'apiBaseUrl',
    );
    gh.lazySingleton<_i522.BoardRepository>(
      () => _i436.ApiBoardRepository(
        gh<_i519.Client>(),
        gh<String>(instanceName: 'apiBaseUrl'),
      ),
    );
    gh.factory<_i314.BoardViewModel>(
      () => _i314.BoardViewModel(gh<_i522.BoardRepository>()),
    );
    return this;
  }
}

class _$NetworkModule extends _i561.NetworkModule {}
