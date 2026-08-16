import { NestFactory } from '@nestjs/core';
import { AppModule } from './app.module';
import { ValidationPipe } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import "dotenv/config";

async function bootstrap() {
  const app = await NestFactory.create(AppModule);

  app.useGlobalPipes(new ValidationPipe({whitelist: true, transform:true}));

  const config= new DocumentBuilder()
    .setTitle("PixelRoad API Document")
    .setDescription("GPS를 기반으로 사용자가 실제 랜드마크를 방문하여 탐험하고 수집하는 교육형 탐험 플랫폼")
    .setVersion("1.0")
    .addBearerAuth()
    .build();

  SwaggerModule.setup("docs", app, SwaggerModule.createDocument(app, config)); 

  await app.listen(process.env.PORT ?? 3000);
  console.log(`PixelRoad 시작: Http://localhost:${process.env.PORT} (Swagger 문서: /docs)`);
}
bootstrap();
