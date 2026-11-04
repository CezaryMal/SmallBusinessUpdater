# Small Business Updater

Hobbystyczna aplikacja Windows do sprawdzania, pobierania i instalowania aktualizacji programu Small Business firmy Symplex. Interfejs jest napisany w WPF na platformie .NET 10.

## Korzystanie z programu

1. Zainstaluj aplikację z pliku SmallBusinessUpdater_Setup_v*.exe.
2. Uruchom Updater i wpisz ID Symplex, NIP oraz datę końca dostępu do aktualizacji. Dane znajdziesz w Small Business w sekcji Inne → O programie → Licencja.
3. Wskaż plik wykonywalny Small Business, np. C:\SB4\ksiegaw.exe.
4. Wybierz opcje autostartu i uruchamiania Small Business po aktualizacji.
5. Zapisz ustawienia i kliknij „Sprawdź aktualizacje”.

Pobrany instalator jest weryfikowany przez Windows. Updater sprawdza również, czy odczytany wydawca zawiera nazwę Symplex. Po udanej instalacji zapisuje numer buildu w state.json.

Aktualizacje są blokowane od początku wpisanego dnia końca dostępu. Jest to celowe zachowanie projektu.

Automatyczna obsługa instalatora Small Business korzysta z okien i skrótów klawiaturowych. Zmiany w interfejsie instalatora Symplex mogą wymagać dostosowania Updatera.

## Tryby uruchamiania

| Argument | Działanie |
|---|---|
| Bez argumentu | Otwiera okno ustawień i ręcznego sprawdzania aktualizacji. |
| --startup | Wyświetla okno sprawdzania, a następnie sprawdza i w razie potrzeby instaluje aktualizację. Uwzględnia opcję uruchamiania Small Business. |
| --check | Sprawdza i w razie potrzeby instaluje aktualizację bez początkowego okna sprawdzania. |

Przy niepełnej konfiguracji tryby automatyczne otwierają ustawienia. Po wygaśnięciu okresu aktualizacji wyświetlane jest odpowiednie okno.

Przykład uruchomienia w PowerShellu przy domyślnym katalogu instalacji dla bieżącego użytkownika

```powershell
& "$env:LOCALAPPDATA\Programs\Small Business Updater\SmallBusinessUpdater.exe" --check
```

Opcja autostartu zapisuje wpis SmallBusinessUpdater w kluczu bieżącego użytkownika HKCU\Software\Microsoft\Windows\CurrentVersion\Run. Wpis uruchamia zainstalowany plik z argumentem --startup po zalogowaniu do Windows.

## Dane lokalne

| Ścieżka | Zawartość |
|---|---|
| %APPDATA%\SmallBusinessUpdater\config.json | Ustawienia licencji, ścieżka programu i opcje aplikacji. |
| %APPDATA%\SmallBusinessUpdater\state.json | Stan ostatniej pomyślnie zainstalowanej aktualizacji Small Business. |
| %LOCALAPPDATA%\SmallBusinessUpdater\Downloads | Instalatory Small Business. |
| %LOCALAPPDATA%\SmallBusinessUpdater\SelfUpdate | Instalatory Updatera, update.lock i install-result.log. |
| %LOCALAPPDATA%\SmallBusinessUpdater\Logs | Logi, w tym startup.log, check.log, self-update.log i cleanup.log. |

Logi są rotowane po osiągnięciu około 2 MiB. Dla każdego pliku przechowywane są maksymalnie trzy archiwalne kopie.

Usunięcie state.json powoduje utratę zapisanego numeru buildu i może skutkować ponownym zaoferowaniem instalacji tej samej aktualizacji. Przed ręczną edycją danych zamknij Updater i wykonaj ich kopię.

## Otwieranie i kompilacja

Do pracy nad projektem potrzebne są Windows x64, SDK .NET 10 oraz Visual Studio 2026 z obsługą aplikacji klasycznych .NET i WPF.

Otwórz SmallBusinessUpdater.slnx lub bezpośrednio SmallBusinessUpdater.csproj. Projekt używa docelowej platformy net10.0-windows10.0.19041.0 i generuje aplikację x64.

Kompilacja z głównego katalogu repozytorium

```powershell
dotnet build SmallBusinessUpdater.csproj -c Release
```

Publikacja samodzielnego EXE z dołączonym środowiskiem .NET

```powershell
dotnet publish SmallBusinessUpdater.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o artifacts/publish
```

Po publikacji skrypt SmallBusinessUpdater.iss można skompilować za pomocą ISCC.exe z Inno Setup 6. Skrypt oczekuje aplikacji w artifacts/publish/SmallBusinessUpdater.exe i zapisuje instalator w artifacts/installer.

## Wersjonowanie

Numer wersji zmienia się w jednym miejscu w SmallBusinessUpdater.csproj, np.

```xml
<Version>0.8.7.2</Version>
```

Kompilacja generuje z tej wartości metadane aplikacji. Inno Setup odczytuje numer z opublikowanego EXE i używa go w nazwie instalatora oraz wpisie zainstalowanej aplikacji.

Okno Updatera wyświetla pełny czteroczłonowy numer, np. v0.8.7.2.

AppId w skrypcie Inno Setup identyfikuje istniejącą instalację. Zachowuj tę wartość przy kolejnych wydaniach, aby instalator aktualizował poprzednią kopię.

## Budowanie na GitHubie

Workflow znajduje się w .github/workflows/build-installer.yml.

1. Wypchnij zmiany na wybraną gałąź.
2. Otwórz Actions → Build Windows installer → Run workflow.
3. Wybierz gałąź, np. Development do testów lub main do wydania.
4. Po udanym wykonaniu pobierz SmallBusinessUpdater_Setup_v*.exe z sekcji Artifacts.

Workflow działa na Windowsie, instaluje SDK .NET 10, publikuje samodzielną aplikację, kompiluje skrypt Inno Setup i udostępnia instalator bez dodatkowego archiwum ZIP.

Gałąź Development służy do przygotowywania i testowania zmian. Ręczne uruchomienie workflow buduje instalator i udostępnia go w Artifacts.

### Automatyczne wydania

Push do main, również powstały po scaleniu pull requesta, uruchamia build. Po udanej kompilacji osobny job publikuje GitHub Release z tagiem v<Version> i instalatorem EXE z tego samego uruchomienia.

1. Na Development zmień Version w pliku .csproj na nowy, czteroczłonowy numer.
2. Wykonaj ręczny build na Development i przetestuj instalator.
3. Scal sprawdzone zmiany do main.
4. Sprawdź wynik jobów build i release oraz załącznik w GitHub Releases.

Wydanie jest najpierw tworzone jako szkic. Po przesłaniu instalatora workflow publikuje je z automatycznie wygenerowanym opisem zmian. Tag wskazuje commit użyty do buildu. Token GITHUB_TOKEN dostaje uprawnienie contents: write wyłącznie w jobie publikującym.

Jeśli opublikowane wydanie o tym numerze już istnieje, publikacja jest pomijana. Każde kolejne wydanie wymaga zwiększenia Version. Przy błędzie przesyłania instalatora szkic pozostaje do ponowienia. Workflow sprawdza zgodność istniejącego tagu z commitem przed wznowieniem publikacji szkicu.

Po opublikowaniu Release instalator staje się dostępny dla mechanizmu samoczynnej aktualizacji działających Updaterów. Przed scaleniem do main przetestuj wersję na Development.

## Samoczynna aktualizacja Updatera

Aplikacja instaluje się dla bieżącego użytkownika w %LOCALAPPDATA%\Programs\Small Business Updater. Instalator ma PrivilegesRequired=lowest. Samodzielny EXE zawiera środowisko .NET.

Po zakończeniu obsługi aktualizacji Small Business, w tym jego uruchomienia zgodnie z zapisaną opcją, aplikacja sprawdza najnowsze stabilne wydanie w publicznym repozytorium CezaryMal/SmallBusinessUpdater. Sprawdzanie może też nastąpić po błędzie operacji Small Business. Wcześniejsze wyjście z powodu nieprawidłowej konfiguracji lub daty końca dostępu pomija tę ścieżkę.

Release musi mieć tag vX.Y.Z.W oraz załącznik SmallBusinessUpdater_Setup_vX.Y.Z.W.exe. Mechanizm porównuje wszystkie cztery człony wersji i weryfikuje rozmiar oraz SHA256 pobranego pliku względem metadanych GitHuba.

Po pobraniu nowszej wersji Updater zamyka się, a proces pomocniczy czeka na jego zakończenie i uruchamia instalator w trybie cichym. Program można otworzyć ponownie po instalacji. Wynik instalatora trafia do SelfUpdate\install-result.log, a szczegóły sprawdzania do Logs\self-update.log. Nieudana próba może zostać ponowiona przy kolejnym sprawdzaniu.

Przejście ze starej instalacji w Program Files wymaga jednorazowego odinstalowania starej kopii i zainstalowania wersji dla bieżącego użytkownika. Przed migracją wykonaj kopię %APPDATA%\SmallBusinessUpdater. Po migracji sprawdź i zapisz ustawienia autostartu.

## Przechowywanie instalatorów

Small Business i Updater mają osobne archiwa.

- Downloads zachowuje po jednym instalatorze trzech najwyższych rozpoznanych buildów Small Business. Sprzątanie następuje po pobraniu i weryfikacji instalatora.
- SelfUpdate zachowuje trzy najwyższe czteroczłonowe wersje instalatora Updatera. Sprzątanie następuje przed żądaniem do GitHuba i po pobraniu nowej wersji, więc działa również przed nieudaną próbą połączenia.
- Plik chroniony podczas operacji lub zablokowany przed usunięciem może chwilowo zwiększyć liczbę kopii. Kolejne sprawdzanie ponawia sprzątanie.
- Sprzątanie obejmuje rozpoznane nazwy instalatorów. Pliki logów, blokady i pliki .part pozostają w katalogu.

## Kontrola przed wydaniem

- Sprawdź numer wersji aplikacji, nazwę instalatora i wpis w ustawieniach Windows.
- Sprawdź aktualizację istniejącej instalacji Updatera oraz zachowanie ustawień.
- Sprawdź ręczne pobieranie i instalowanie aktualizacji Small Business.
- Sprawdź tryby --startup i --check.
- Potwierdź zapis state.json i rozpoznanie zainstalowanego buildu przy ponownym sprawdzeniu.
- Przejrzyj wynik workflow i logi aplikacji.

## Repozytorium

Przechowuj w repozytorium źródła C#, XAML, ikonę, projekt, rozwiązanie, skrypt Inno Setup i workflow.

Lokalne katalogi .vs, bin i obj są wykluczane przez .gitignore. Przed commitem lokalnego buildu sprawdź również, czy wygenerowany katalog artifacts jest wykluczony.

## Podpis instalatora

Instalator Updatera jest obecnie dystrybuowany bez podpisu cyfrowego. Windows może wyświetlić komunikat SmartScreen o nierozpoznanej aplikacji i nieznanym wydawcy.


