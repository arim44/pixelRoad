import { Injectable } from '@nestjs/common';

@Injectable()
export class AppService {
   getHello() {
    const hfToken = process.env.HF_TOKEN ? '설정됨' : '미설정';
    const hfModel = process.env.HF_MODEL ? '설정됨' : '미설정';

    return {
      message: 'Hello World!-app service',
      HF_TOKEN: hfToken,
      HF_MODEL: hfModel,
    };
  }
}