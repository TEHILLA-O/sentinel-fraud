# Rules

| Code | Contextual inputs | Default score |
| --- | --- | --- |
| HIGH_VALUE | Amount vs configurable threshold | 20, scaled if 2x threshold |
| HIGH_VELOCITY | Redis 1m/5m counts | 30 plus overflow |
| FOREIGN_LOCATION | Home + common countries | 20 |
| UNKNOWN_DEVICE | Known devices / common devices | 15 |
| CARD_NOT_PRESENT | Channel + card present. Transfers ignored | 10 |
| IMPOSSIBLE_TRAVEL | Previous physical POS/ATM location vs current | 40 |
| MERCHANT_RISK | MCC vs customer norms | 15 |
| UNUSUAL_TIME | Typical hours UTC | 10 |
| NEW_ACCOUNT | Account age | 15 |
| BEHAVIOURAL_DEVIATION | Median/average spend, needs ≥5 historic tx | 15 |
| RAPID_COUNTRY_CHANGE | Distinct countries in 30 minutes | 20 |
| REPEATED_DECLINE | Failed tx in 15 minutes | 20 |
| ROUND_AMOUNT | Round high-value amounts | 8 |

## Impossible travel assumptions

- Only POS and ATM require physical presence. WEB, MOBILE and TRANSFER are ignored.
- Cities are used when known; otherwise a country centroid is used (US ≈ New York, GB ≈ London).
- Maximum implied speed defaults to 900 km/h plus the elapsed time between the two events.
- This is an approximation, not a geolocation product.
