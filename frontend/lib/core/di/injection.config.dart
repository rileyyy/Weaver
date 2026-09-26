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
import 'package:weaver/features/auth/auth_view_model.dart' as _i6;
import 'package:weaver/features/auth/data/api_auth_repository.dart' as _i461;
import 'package:weaver/features/auth/data/auth_repository.dart' as _i899;
import 'package:weaver/features/auth/data/auth_session_store.dart' as _i605;
import 'package:weaver/features/auth/data/secure_token_store.dart' as _i54;
import 'package:weaver/features/board/board_view_model.dart' as _i314;
import 'package:weaver/features/board/data/api_board_repository.dart' as _i436;
import 'package:weaver/features/board/data/board_repository.dart' as _i522;
import 'package:weaver/features/work_item_detail/data/api_work_item_detail_repository.dart'
    as _i335;
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart'
    as _i982;
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart'
    as _i194;
import 'package:weaver/shared/data/api_status_repository.dart' as _i209;
import 'package:weaver/shared/data/api_user_directory_repository.dart' as _i982;
import 'package:weaver/shared/data/current_user.dart' as _i875;
import 'package:weaver/shared/data/status_repository.dart' as _i770;
import 'package:weaver/shared/data/user_directory_repository.dart' as _i562;

extension GetItInjectableX on _i174.GetIt {
  // initializes the registration of main-scope dependencies inside of GetIt
  _i174.GetIt init({
    String? environment,
    _i526.EnvironmentFilter? environmentFilter,
  }) {
    final gh = _i526.GetItHelper(this, environment, environmentFilter);
    final networkModule = _$NetworkModule();
    gh.lazySingleton<_i54.SecureTokenStore>(
      () => _i54.FlutterSecureTokenStore(),
    );
    gh.lazySingleton<_i519.Client>(
      () => networkModule.rawHttpClient,
      instanceName: 'rawHttpClient',
    );
    gh.factory<String>(
      () => networkModule.apiBaseUrl,
      instanceName: 'apiBaseUrl',
    );
    gh.lazySingleton<_i899.AuthRepository>(
      () => _i461.ApiAuthRepository(
        gh<_i519.Client>(instanceName: 'rawHttpClient'),
        gh<String>(instanceName: 'apiBaseUrl'),
      ),
    );
    gh.lazySingleton<_i605.AuthSessionStore>(
      () => _i605.AuthSessionStore(
        gh<_i899.AuthRepository>(),
        gh<_i54.SecureTokenStore>(),
      ),
    );
    gh.lazySingleton<_i519.Client>(
      () => networkModule.httpClient(
        gh<_i519.Client>(instanceName: 'rawHttpClient'),
        gh<_i605.AuthSessionStore>(),
      ),
    );
    gh.lazySingleton<_i770.StatusRepository>(
      () => _i209.ApiStatusRepository(
        gh<_i519.Client>(),
        gh<String>(instanceName: 'apiBaseUrl'),
      ),
    );
    gh.factory<_i875.CurrentUser>(
      () => networkModule.currentUser(gh<_i605.AuthSessionStore>()),
    );
    gh.lazySingleton<_i562.UserDirectoryRepository>(
      () => _i982.ApiUserDirectoryRepository(
        gh<_i519.Client>(),
        gh<String>(instanceName: 'apiBaseUrl'),
      ),
    );
    gh.factory<_i6.AuthViewModel>(
      () => _i6.AuthViewModel(
        gh<_i899.AuthRepository>(),
        gh<_i605.AuthSessionStore>(),
      ),
    );
    gh.lazySingleton<_i982.WorkItemDetailRepository>(
      () => _i335.ApiWorkItemDetailRepository(
        gh<_i519.Client>(),
        gh<String>(instanceName: 'apiBaseUrl'),
        gh<_i770.StatusRepository>(),
        gh<_i562.UserDirectoryRepository>(),
      ),
    );
    gh.factory<_i194.WorkItemDetailViewModel>(
      () => _i194.WorkItemDetailViewModel(
        gh<_i982.WorkItemDetailRepository>(),
        gh<_i875.CurrentUser>(),
      ),
    );
    gh.lazySingleton<_i522.BoardRepository>(
      () => _i436.ApiBoardRepository(
        gh<_i519.Client>(),
        gh<String>(instanceName: 'apiBaseUrl'),
        gh<_i770.StatusRepository>(),
        gh<_i562.UserDirectoryRepository>(),
      ),
    );
    gh.factory<_i314.BoardViewModel>(
      () => _i314.BoardViewModel(gh<_i522.BoardRepository>()),
    );
    return this;
  }
}

class _$NetworkModule extends _i561.NetworkModule {}
