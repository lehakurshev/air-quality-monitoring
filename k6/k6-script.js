import http from 'k6/http'
import { check, sleep } from 'k6'

sleep(20)  // ждем 20 секунд пока backend стартует

export const options = {
  scenarios: {
    load: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "2m", target: 1000 },
        { duration: "58m", target: 1000 }
      ]
    }
  }
}

const BASE_URL = 'http://backend:8080'

// центр
const BASE_LAT = 56.8333
const BASE_LON = 60.5833
const OFFSET = 0.05

function randomString(length) {
  const chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
  let result = ''
  for (let i = 0; i < length; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length))
  }
  return result
}

export default function () {

  // уникальная точка устройства
  const latitude = BASE_LAT + (Math.random() * 2 - 1) * OFFSET
  const longitude = BASE_LON + (Math.random() * 2 - 1) * OFFSET

  const email = randomString(20)
  const password = randomString(20)

  // REGISTER
  const registerRes = http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  )

  check(registerRes, {
    'register ok': (r) => r.status === 200
  })

  const apiToken = registerRes.json().apiToken

  // два цикла по 30 минут
  for (let tokenCycle = 0; tokenCycle < 2; tokenCycle++) {

    // получить access token
    const tokenRes = http.post(
      `${BASE_URL}/api/auth/token`,
      JSON.stringify({ apiToken }),
      { headers: { 'Content-Type': 'application/json' } }
    )

    check(tokenRes, {
      'token ok': (r) => r.status === 200
    })

    const accessToken = tokenRes.json().accessToken

    const headers = {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`
      }
    }

    // 30 measurement запросов
    for (let i = 0; i < 30; i++) {

      const payload = JSON.stringify({
        co: Math.random(),
        no2: Math.random(),
        pm25: Math.random() * 50,
        pm10: Math.random() * 50,
        latitude,
        longitude
      })

      const res = http.post(
        `${BASE_URL}/api/measurement`,
        payload,
        headers
      )

      check(res, {
        'measurement ok': (r) => r.status === 200
      })

      sleep(60) // примерно раз в минуту
    }

    // ждём до истечения токена
    sleep(1800) // 30 минут
  }
}