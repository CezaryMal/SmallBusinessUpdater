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

Przykład uruchomienia w PowerShellu przy domyślnym katalogu instalacji

```powershell
& 'C:\Program Files\Small Business Updater\SmallBusinessUpdater.exe' --check
```

Opcja autostartu zapisuje wpis SmallBusinessUpdater w kluczu bieżącego użytkownika HKCU\Software\Microsoft\Windows\CurrentVersion\Run. Wpis uruchamia zainstalowany plik z argumentem --startup po zalogowaniu do Windows.

## Dane lokalne

| Ścieżka | Zawartość |
|---|---|
| %APPDATA%\SmallBusinessUpdater\config.json | Ustawienia licencji, ścieżka programu i opcje aplikacji. |
| %APPDATA%\SmallBusinessUpdater\state.json | Stan ostatniej pomyślnie zainstalowanej aktualizacji Small Business. |
| %LOCALAPPDATA%\SmallBusinessUpdater\Downloads | Pobrane instalatory. |
| %LOCALAPPDATA%\SmallBusinessUpdater\Logs | Logi, w tym startup.log i check.log. |

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
<Version>0.8.6.3</Version>
```

Kompilacja generuje z tej wartości metadane aplikacji. Inno Setup odczytuje numer z opublikowanego EXE i używa go w nazwie instalatora oraz wpisie zainstalowanej aplikacji.

Okno Updatera wyświetla trzy pierwsze człony numeru. Dla 0.8.6.3 interfejs pokazuje v0.8.6.

AppId w skrypcie Inno Setup identyfikuje istniejącą instalację. Zachowuj tę wartość przy kolejnych wydaniach, aby instalator aktualizował poprzednią kopię.

## Budowanie na GitHubie

Workflow znajduje się w .github/workflows/build-installer.yml.

1. Wypchnij zmiany na wybraną gałąź.
2. Otwórz Actions → Build Windows installer → Run workflow.
3. Wybierz gałąź, np. Development do testów lub main do wydania.
4. Po udanym wykonaniu pobierz SmallBusinessUpdater_Setup_v*.exe z sekcji Artifacts.

Workflow działa na Windowsie, instaluje SDK .NET 10, publikuje samodzielną aplikację, kompiluje skrypt Inno Setup i udostępnia instalator bez dodatkowego archiwum ZIP.

Gałąź Development służy do przygotowywania i testowania zmian. Zmiany gotowe do wydania są scalane do main. Workflow jest uruchamiany ręcznie.

Gotowy instalator można ręcznie dołączyć do GitHub Releases z tagiem odpowiadającym wersji, np. v0.8.6.3, wskazującym na commit użyty do buildu.

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

