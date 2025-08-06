# Jak Współtworzyć Synapse AI

Cieszymy się, że interesuje Cię rozwój Synapse AI! Każda pomoc jest mile widziana. Aby proces był płynny i efektywny dla wszystkich, prosimy o przestrzeganie poniższych wytycznych.

## Kodeks Postępowania

Ten projekt i wszyscy jego uczestnicy podlegają naszemu [Kodeksowi Postępowania](CODE_OF_CONDUCT.md). Biorąc udział, zobowiązujesz się do przestrzegania jego zasad.

## Jak Możesz Pomóc?

- **Zgłaszanie Błędów:** Jeśli znajdziesz błąd, sprawdź, czy nie został on już zgłoszony w sekcji [Issues](https://github.com/TWOJ_USERNAME/SynapseAI/issues). Jeśli nie, utwórz nowe zgłoszenie, dołączając szczegółowy opis, kroki do odtworzenia błędu i informacje o swoim środowisku (system operacyjny, wersja aplikacji).

- **Propozycje Nowych Funkcji:** Masz pomysł na nową funkcję lub ulepszenie? Otwórz nowe zgłoszenie w sekcji [Issues](https://github.com/TWOJ_USERNAME/SynapseAI/issues), abyśmy mogli je przedyskutować. Opisz jasno, jaki problem rozwiązuje Twoja propozycja i jak miałaby działać.

- **Pull Requests:** Chętnie przyjmujemy Pull Requests! Jeśli chcesz dodać nową funkcję lub naprawić błąd, postępuj zgodnie z poniższą procedurą.

## Proces Przesyłania Zmian (Pull Request)

1.  **Sforkuj repozytorium:** Stwórz własną kopię projektu na swoim koncie GitHub.

2.  **Stwórz nową gałąź (branch):**
    ```bash
    git checkout -b twoja-nazwa-funkcji-lub-poprawki
    ```
    Używaj opisowych nazw gałęzi, np. `feature/add-chat-export` lub `fix/crash-on-android`.

3.  **Wprowadź zmiany:** Pisz czysty i czytelny kod, zgodny ze stylem istniejącego projektu. Dodawaj komentarze tylko tam, gdzie jest to absolutnie konieczne do zrozumienia skomplikowanej logiki.

4.  **Upewnij się, że projekt się buduje:** Przed wysłaniem zmian, upewnij się, że projekt kompiluje się bez błędów na docelowych platformach.

5.  **Commituj zmiany:** Używaj jasnych i opisowych komunikatów commitów. Dobrą praktyką jest stosowanie konwencji [Conventional Commits](https://www.conventionalcommits.org/).
    ```bash
    git commit -m "feat: Dodano eksport konwersacji do formatu Markdown"
    ```

6.  **Wypchnij zmiany na swoje sforkowane repozytorium:**
    ```bash
    git push origin twoja-nazwa-funkcji-lub-poprawki
    ```

7.  **Otwórz Pull Request:** Przejdź do oryginalnego repozytorium Synapse AI i utwórz nowy Pull Request. W opisie wyjaśnij, co Twoje zmiany wprowadzają i dlaczego są potrzebne. Jeśli Twój PR rozwiązuje istniejące zgłoszenie (issue), dodaj w opisie `Closes #123`.

## Standardy Kodowania

- **C# / .NET:** Trzymaj się standardowych konwencji nazewnictwa i formatowania .NET. Używaj `async/await` dla operacji I/O. Staraj się pisać kod asynchroniczny tam, gdzie to możliwe, aby nie blokować wątku UI.
- **XAML:** Utrzymuj czystą i czytelną strukturę. Unikaj definiowania kolorów i stylów bezpośrednio w kontrolkach – używaj zasobów (`ResourceDictionary`).
- **Rust:** Postępuj zgodnie z oficjalnymi wytycznymi formatowania (`cargo fmt`).

Dziękujemy za Twój wkład w rozwój Synapse AI!