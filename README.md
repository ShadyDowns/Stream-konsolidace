# ZombieLilčin konsolidátor odpovědí

Jednoduchá Windows aplikace pro moderované čtení Twitch chatu. Každý divák má
v levém sloupci jednu kartu se svou historií. Vpravo je vždy nejstarší dosud
nevyřízená zpráva a tlačítko **Další zpráva** posune frontu dál.

## Rychlé spuštění

Hotová přenosná verze je po sestavení v:

```text
publish\win-x64\ZombieLilčin konsolidátor odpovědí.exe
```

Jde o samostatný soubor pro 64bitový Windows; cílový počítač nepotřebuje
instalovat .NET. Tlačítkem **Demo** lze celé rozhraní vyzkoušet offline.

## Připojení k Twitch chatu

Vyplňte:

1. název kanálu bez `twitch.tv/` a bez `#`,
2. přihlašovací jméno Twitch účtu,
3. uživatelský OAuth access token stejného účtu se scope `chat:read`.

Potom zvolte **Připojit**. Token aplikace nikdy neukládá; v lokálním nastavení
zůstane pouze naposledy použitý kanál a jméno účtu. Nápovědu k oficiálnímu OAuth
postupu otevře tlačítko **?** nebo dokumentace Twitch:

<https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/>

Aplikace chat pouze čte. Neodesílá zprávy, nemoderuje a nepotřebuje scope
`chat:edit`.

## Sestavení ze zdrojů

Požadavky: Windows a .NET SDK 10.

```powershell
.\build-release.ps1
```

Skript sestaví řešení, spustí testy parseru/fronty a vytvoří samostatné `.exe`.
Součástí sestavení je vícerozměrná Windows ikona vygenerovaná z projektového
zdroje `StreamKartoteka\Assets\ZombieLilcin-icon-source.png`.

## Princip fronty

- Každá příchozí zpráva vstoupí do jedné globální FIFO fronty.
- Více zpráv téhož člověka se sloučí do jedné uživatelské karty.
- **Další zpráva** označí právě zobrazenou zprávu za vyřízenou.
- Vyřízené zprávy zůstanou v historii uživatele zeslabené.
- Při větším počtu uživatelů se levé karty automaticky překrývají jako kartotéka.
